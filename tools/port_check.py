import argparse
import re
import sys
from collections import defaultdict
from pathlib import Path

import dnfile
import pefile
from dncil.cil.body import CilMethodBody
from dncil.cil.body.reader import CilMethodBodyReaderBase
from dncil.clr.token import StringToken, Token

ABOUT = "Checks a PC MelonLoader mod for A Township Tale for what won't carry over to the Quest (IL2CPP) build."

MODULE, TYPEREF, TYPEDEF, FIELD, METHOD, MEMBERREF = 0x00, 0x01, 0x02, 0x04, 0x06, 0x0A
MODULEREF, TYPESPEC, ASSEMBLYREF, METHODSPEC = 0x1A, 0x1B, 0x23, 0x2B
TABLE_IDS = {"Module": MODULE, "TypeRef": TYPEREF, "TypeDef": TYPEDEF, "Field": FIELD, "MethodDef": METHOD,
             "MemberRef": MEMBERREF, "ModuleRef": MODULEREF, "TypeSpec": TYPESPEC,
             "AssemblyRef": ASSEMBLYREF, "MethodSpec": METHODSPEC}

LOADER = {"MelonLoader", "0Harmony", "Mono.Cecil", "MonoMod.RuntimeDetour", "MonoMod.Utils",
          "Il2CppInterop.Runtime", "Il2CppInterop.Common"}
RUNTIME = re.compile(r"^(mscorlib|netstandard|System|System\..+|Microsoft\..+|Mono\..+)$")
MELON_BASES = {"MelonLoader.MelonMod", "MelonLoader.MelonPlugin", "MelonLoader.MelonBase"}
MELON_FRAME = {"OnUpdate", "OnFixedUpdate", "OnLateUpdate", "OnGUI"}
UNITY_FRAME = {"Update", "FixedUpdate", "LateUpdate", "OnGUI"}
PATCH_ATTRS = {"HarmonyLib.HarmonyPrefix": "Prefix", "HarmonyLib.HarmonyPostfix": "Postfix",
               "HarmonyLib.HarmonyTranspiler": "Transpiler", "HarmonyLib.HarmonyFinalizer": "Finalizer"}
METHOD_TYPES = {1: "getter", 2: "setter", 3: "constructor", 4: "static constructor", 5: "enumerator", 6: "async"}
REFLECTION = {
    "System.Type": {"GetField", "GetFields", "GetMethod", "GetMethods", "GetProperty", "GetProperties",
                    "GetMember", "GetMembers", "GetNestedType", "GetEvent", "InvokeMember"},
    "System.Reflection.Assembly": {"GetType"},
    "HarmonyLib.AccessTools": {"Field", "DeclaredField", "Method", "DeclaredMethod", "Property",
                               "DeclaredProperty", "PropertyGetter", "PropertySetter", "DeclaredPropertyGetter",
                               "DeclaredPropertySetter", "Constructor", "DeclaredConstructor", "TypeByName",
                               "FieldRefAccess", "StaticFieldRefAccess", "MethodDelegate", "Inner",
                               "GetDeclaredFields", "GetDeclaredMethods", "GetDeclaredProperties"},
    "HarmonyLib.Traverse": {"Create", "Field", "Property", "Method"},
}
PATCH_LOOKUPS = {"Method", "DeclaredMethod", "PropertyGetter", "PropertySetter", "DeclaredPropertyGetter",
                 "DeclaredPropertySetter", "Constructor", "DeclaredConstructor"}
CROSSING = re.compile(r"^System\.(Collections\..+|Action(`\d+)?|Func`\d+|Predicate`1|Comparison`1|EventHandler(`1)?|"
                      r"Delegate|MulticastDelegate|Type|IO\.(Stream|MemoryStream|BinaryReader|BinaryWriter)|"
                      r"Threading\.Tasks\.(Task|ValueTask)(`1)?)$")
SERVER_MARKS = {"/start_server", "get_IsServer", "IsServer", "get_IsHeadless", "IsHeadless", "StartServerArgument"}
SMALL_IL = 24
SEVERITIES = ("blocker", "rewrite", "risk", "info")

GENERIC_ARGS = re.compile(r"(?<=[\w\]])<[^<>]*>")


def bare(name):
    name = re.sub(r"`\d+", "", name.replace("/", ".").replace("+", "."))
    prev = None
    while prev != name:
        prev, name = name, GENERIC_ARGS.sub("", name)
    return name


def pretty(name):
    return re.sub(r"`\d+", "", name)


def unprefix(name):
    if name.startswith("Il2Cpp."):
        return name[7:]
    if re.match(r"Il2Cpp[A-Z]", name):
        return name[6:]
    return name


def is_unity(asm):
    return asm.startswith("UnityEngine") or asm.startswith("Unity.")


def quest_name(asm):
    return asm if is_unity(asm) else "Il2Cpp" + asm


def blob(item):
    value = getattr(item, "value", item)
    return bytes(value) if value is not None else b""


def cidx(ci):
    if ci is None or getattr(ci, "table", None) is None or not getattr(ci, "row_index", 0):
        return None
    table = TABLE_IDS.get(type(ci.table).__name__)
    return (table, ci.row_index) if table is not None else None


def rows(md, name):
    table = getattr(md, name, None)
    return table.rows if table is not None else []


TDOR, TOMD, SCOPE = (TYPEDEF, TYPEREF, TYPESPEC), (TYPEDEF, METHOD), (MODULE, MODULEREF, ASSEMBLYREF, TYPEREF)


def coded(value, tables):
    bits = (len(tables) - 1).bit_length()
    tag, rid = value & ((1 << bits) - 1), value >> bits
    return (tables[tag], rid) if tag < len(tables) and rid else None


def spans(md, owners, attr, target):
    starts = [getattr(r.struct, attr) for r in rows(md, owners)]
    end = len(rows(md, target)) + 1
    return [range(s, max(s, starts[i + 1] if i + 1 < len(starts) else end)) for i, s in enumerate(starts)]


class Sig:
    def __init__(self, data):
        self.b, self.i = data, 0

    def u8(self):
        value = self.b[self.i]
        self.i += 1
        return value

    def int32(self):
        value = int.from_bytes(self.b[self.i:self.i + 4], "little", signed=True)
        self.i += 4
        return value

    def uint(self):
        first = self.u8()
        if first & 0x80 == 0:
            return first
        if first & 0xC0 == 0x80:
            return ((first & 0x3F) << 8) | self.u8()
        return ((first & 0x1F) << 24) | (self.u8() << 16) | (self.u8() << 8) | self.u8()

    def tdor(self):
        value = self.uint()
        return (TYPEDEF, TYPEREF, TYPESPEC)[value & 3], value >> 2

    def type(self):
        e = self.u8()
        while e in (0x1F, 0x20):
            self.tdor()
            e = self.u8()
        if e in (0x0F, 0x10, 0x45):
            return self.type()
        if e in (0x11, 0x12):
            return ("ref", self.tdor())
        if e == 0x15:
            self.u8()
            generic = self.tdor()
            return ("generic", generic, [self.type() for _ in range(self.uint())])
        if e == 0x1D:
            return ("array", self.type())
        if e == 0x14:
            element = self.type()
            self.uint()
            for _ in range(self.uint()):
                self.uint()
            for _ in range(self.uint()):
                self.uint()
            return ("array", element)
        if e in (0x13, 0x1E):
            self.uint()
            return ("var",)
        if e == 0x1B:
            self.method()
            return ("fnptr",)
        return ("prim", e)

    def method(self):
        cc = self.u8()
        if cc & 0x10:
            self.uint()
        count = self.uint()
        ret = self.type()
        params = []
        for _ in range(count):
            if self.b[self.i] == 0x41:
                self.i += 1
            params.append(self.type())
        return ret, params

    def ser_string(self):
        if self.b[self.i] == 0xFF:
            self.i += 1
            return None
        size = self.uint()
        text = self.b[self.i:self.i + size].decode("utf-8", "replace")
        self.i += size
        return text


def walk(node, out):
    if node[0] == "ref":
        out.append(node[1])
    elif node == ("prim", 0x1C):
        out.append("object")
    elif node[0] == "array":
        out.append("array")
        walk(node[1], out)
    elif node[0] == "generic":
        out.append(node[1])
        for arg in node[2]:
            walk(arg, out)
    return out


class Reader(CilMethodBodyReaderBase):
    def __init__(self, pe, rva):
        self.pe = pe
        self.offset = pe.get_offset_from_rva(rva)

    def read(self, n):
        data = self.pe.get_data(self.pe.get_rva_from_offset(self.offset), n)
        self.offset += n
        return data

    def tell(self):
        return self.offset

    def seek(self, offset):
        self.offset = offset
        return offset


def read_types(asm):
    md = asm.md
    props, events = defaultdict(set), defaultdict(set)
    for table, attr, target, out in (("PropertyMap", "PropertyList_Index", "Property", props),
                                     ("EventMap", "EventList_Index", "Event", events)):
        names = rows(md, target)
        for row, span in zip(rows(md, table), spans(md, table, attr, target)):
            out[row.struct.Parent_Index] |= {str(names[i - 1].Name) for i in span}
    generic_types, generic_methods = set(), set()
    for row in rows(md, "GenericParam"):
        owner = coded(row.struct.Owner_CodedIndex, TOMD)
        if owner:
            (generic_types if owner[0] == TYPEDEF else generic_methods).add(owner[1])
    bases = defaultdict(list)
    for rid, row in enumerate(rows(md, "TypeDef"), 1):
        ext = coded(row.struct.Extends_CodedIndex, TDOR)
        found = asm.type_of(ext) if ext else None
        if found:
            bases[rid].append(found[1])
    for row in rows(md, "InterfaceImpl"):
        found = asm.type_of(coded(row.struct.Interface_CodedIndex, TDOR))
        if found:
            bases[row.struct.Class_Index].append(found[1])
    fields, methods = rows(md, "Field"), rows(md, "MethodDef")
    field_spans = spans(md, "TypeDef", "FieldList_Index", "Field")
    types = {}
    for rid in range(1, len(rows(md, "TypeDef")) + 1):
        own = asm.methods_of[rid - 1]
        members = {"F" + str(fields[i - 1].Name) for i in field_spans[rid - 1]}
        members |= {"P" + p for p in props[rid]} | {"E" + e for e in events[rid]}
        members |= {"M" + str(methods[i - 1].Name) for i in own}
        types[unprefix(bare(asm.typedef_names[rid]))] = {
            "bases": [unprefix(bare(b)) for b in bases[rid]], "m": members, "generic": rid in generic_types,
            "gm": {str(methods[i - 1].Name) for i in own if i in generic_methods}}
    return types


class Assemblies:
    def __init__(self, folder, rename=lambda asm: asm):
        self.folder, self.rename = folder, rename
        self.files = {p.stem for p in folder.glob("*.dll")} if folder and folder.is_dir() else set()
        self.mods, self.loaded = {}, {}
        self.extra = []

    def has(self, asm):
        return self.rename(asm) in self.files

    def mod(self, asm):
        file = self.rename(asm)
        if file not in self.files:
            return None
        if file not in self.mods:
            try:
                self.mods[file] = Mod(self.folder / f"{file}.dll")
            except Exception:
                self.mods[file] = None
        return self.mods[file]

    def type(self, asm, name):
        file = self.rename(asm)
        if file not in self.loaded:
            game = self.mod(asm)
            self.loaded[file] = read_types(game) if game else {}
        return self.loaded[file].get(name)

    def lookup(self, asm, name, keys, depth=0):
        found = self.type(asm, name)
        if found is None:
            return None
        if keys is None or keys & found["m"]:
            return True
        if depth < 12:
            for base in found["bases"]:
                for other in [asm] + [a for a in self.extra if a != asm]:
                    if self.type(other, base) is not None:
                        if self.lookup(other, base, keys, depth + 1):
                            return True
                        break
        return False


def member_keys(name, is_field):
    if is_field:
        return {"F" + name, "P" + name}
    if name in (".ctor", ".cctor"):
        return None
    for prefix, kind in (("get_", "P"), ("set_", "P"), ("add_", "E"), ("remove_", "E")):
        if name.startswith(prefix):
            return {kind + name[len(prefix):], "M" + name}
    return {"M" + name}


def patch_keys(name, method_type):
    if method_type in (3, 4) or not name:
        return None
    if method_type in (1, 2):
        return {"P" + name, "M" + ("get_" if method_type == 1 else "set_") + name}
    return member_keys(name, False)


class Report:
    def __init__(self):
        self.items = defaultdict(set)

    def add(self, severity, message, where=()):
        self.items[(severity, message)].update(w for w in where if w)

    def count(self, severity):
        return sum(1 for s, _ in self.items if s == severity)

    def print(self):
        for severity in SEVERITIES:
            found = sorted((m, w) for (s, m), w in self.items.items() if s == severity)
            if not found:
                continue
            print(f"\n{severity.upper()}S" if severity != "info" else "\nINFO")
            for message, where in found:
                print(f"  {message}")
                if where:
                    shown = sorted(where)
                    more = f", and {len(shown) - 4} more" if len(shown) > 4 else ""
                    print(f"      in {', '.join(shown[:4])}{more}")


class Mod:
    def __init__(self, path):
        self.pe = dnfile.dnPE(str(path), clr_lazy_load=True)
        if self.pe.net is None:
            raise ValueError("not a .NET assembly")
        self.md = self.pe.net.mdtables
        self.name = str(rows(self.md, "Assembly")[0].Name)
        self.refs = [str(r.Name) for r in rows(self.md, "AssemblyRef")]
        self.typedef_names = self._typedef_names()
        self.methods_of = spans(self.md, "TypeDef", "MethodList_Index", "MethodDef")
        self.method_owner = {i: rid for rid, span in enumerate(self.methods_of, 1) for i in span}
        self.uses = defaultdict(set)
        self.lookups = []
        self.strings = set()
        self.unreadable = 0

    def _typedef_names(self):
        defs = rows(self.md, "TypeDef")
        outer = {n.struct.NestedClass_Index: n.struct.EnclosingClass_Index for n in rows(self.md, "NestedClass")}
        names = {}

        def full(rid):
            if rid not in names:
                row = defs[rid - 1]
                if rid in outer:
                    names[rid] = f"{full(outer[rid])}.{row.TypeName}"
                else:
                    ns = str(row.TypeNamespace)
                    names[rid] = f"{ns}.{row.TypeName}" if ns else str(row.TypeName)
            return names[rid]

        for rid in range(1, len(defs) + 1):
            full(rid)
        return names

    def typeref(self, rid):
        row = rows(self.md, "TypeRef")[rid - 1]
        ns = str(row.TypeNamespace)
        name = f"{ns}.{row.TypeName}" if ns else str(row.TypeName)
        scope = coded(row.struct.ResolutionScope_CodedIndex, SCOPE)
        if scope and scope[0] == TYPEREF:
            asm, outer = self.typeref(scope[1])
            return asm, f"{outer}.{row.TypeName}"
        if scope and scope[0] == ASSEMBLYREF:
            return str(rows(self.md, "AssemblyRef")[scope[1] - 1].Name), name
        return self.name, name

    def type_of(self, tok):
        if tok is None or tok == "array":
            return None
        table, rid = tok
        if table == TYPEREF:
            return self.typeref(rid)
        if table == TYPEDEF:
            return self.name, self.typedef_names[rid]
        if table == TYPESPEC:
            node = Sig(blob(rows(self.md, "TypeSpec")[rid - 1].Signature)).type()
            if node[0] in ("ref", "generic"):
                return self.type_of(node[1])
        return None

    def memberref(self, rid):
        row = rows(self.md, "MemberRef")[rid - 1]
        data = blob(row.Signature)
        return self.type_of(cidx(row.Class)), str(row.Name), data

    def method_name(self, rid):
        owner = self.typedef_names.get(self.method_owner.get(rid), "?")
        name = str(rows(self.md, "MethodDef")[rid - 1].Name)
        machine = re.match(r"(.*)\.<(\w+)>d__\d+$", owner)
        if machine:
            return f"{machine.group(1)}.{machine.group(2)} (async/iterator)"
        lam = re.match(r"<(\w+)>b__", name)
        if lam:
            outer = re.sub(r"\.<>c(__DisplayClass[\w_]+)?$", "", owner)
            return f"{outer}.{lam.group(1)} (lambda)"
        return f"{owner}.{name}"

    def side(self, asm):
        if asm == self.name:
            return "mod"
        if asm in LOADER:
            return "loader"
        if RUNTIME.match(asm):
            return "runtime"
        if is_unity(asm) or asm in GAME_ASMS:
            return "game"
        return "library"

    def scan(self):
        user = self.pe.net.user_strings
        for rid, row in enumerate(rows(self.md, "MethodDef"), 1):
            if not row.Rva:
                continue
            try:
                body = CilMethodBody(Reader(self.pe, row.Rva))
            except Exception:
                self.unreadable += 1
                continue
            where = self.method_name(rid)
            last_type = last_str = None
            found, patches = [], False
            for ins in body.instructions:
                op = ins.operand
                if isinstance(op, StringToken):
                    text = getattr(user.get(op.rid), "value", None)
                    if isinstance(text, (bytes, bytearray)):
                        text = bytes(text).decode("utf-16le", "replace")
                    last_str = text if isinstance(text, str) else None
                    if last_str:
                        self.strings.add(last_str)
                elif isinstance(op, Token):
                    tok = (op.table, op.rid)
                    if op.table == METHODSPEC:
                        tok = cidx(rows(self.md, "MethodSpec")[op.rid - 1].Method)
                    if tok is None:
                        continue
                    self.uses[tok].add(where)
                    if ins.opcode.name == "ldtoken" and tok[0] in (TYPEREF, TYPEDEF, TYPESPEC):
                        last_type = tok
                    elif ins.opcode.name in ("call", "callvirt", "newobj") and tok[0] == MEMBERREF:
                        owner, name, _ = self.memberref(tok[1])
                        if owner is None:
                            continue
                        if name in SERVER_MARKS:
                            self.strings.add(name)
                        if name in REFLECTION.get(owner[1], ()):
                            found.append((owner[1], name, last_type, last_str))
                            last_type = last_str = None
                        elif owner[1] == "HarmonyLib.Harmony" and name == "Patch":
                            patches = True
            self.lookups += [(where, *f, patches) for f in found]

    def attr_args(self, ctor_rid, value):
        _, params = Sig(blob(rows(self.md, "MemberRef")[ctor_rid - 1].Signature)).method()
        data = Sig(value)
        if len(value) < 2 or data.u8() != 1 or data.u8() != 0:
            return []
        try:
            return [self.fixed_arg(data, p) for p in params]
        except (ValueError, IndexError):
            return []

    def fixed_arg(self, data, node):
        if node[0] == "prim":
            if node[1] == 0x0E:
                return data.ser_string()
            size = {0x02: 1, 0x03: 2, 0x04: 1, 0x05: 1, 0x06: 2, 0x07: 2, 0x08: 4, 0x09: 4, 0x0A: 8, 0x0B: 8,
                    0x0C: 4, 0x0D: 8}.get(node[1])
            if size is None:
                raise ValueError("boxed argument")
            data.i += size
            return None
        if node[0] == "ref":
            found = self.type_of(node[1])
            if found and found[1] == "System.Type":
                return ("type", data.ser_string())
            return ("enum", data.int32())
        if node[0] == "array":
            count = data.int32()
            return [self.fixed_arg(data, node[1]) for _ in range(max(count, 0))]
        raise ValueError("argument kind")


def type_string(text):
    if not text:
        return None
    text = re.sub(r"\[\[.*\]\]", "", text)
    parts = [p.strip() for p in text.split(",")]
    return (parts[1] if len(parts) > 1 else None), bare(parts[0])


def patch_info(args):
    info = {}
    for arg in args:
        if isinstance(arg, tuple) and arg[0] == "type" and "type" not in info:
            info["type"] = type_string(arg[1])
        elif isinstance(arg, str) and "name" not in info:
            info["name"] = arg
        elif isinstance(arg, tuple) and arg[0] == "enum" and "mt" not in info:
            info["mt"] = arg[1]
    return info


def il_sizes(asm, type_name, method):
    game = PC.mod(asm)
    if game is None:
        return []
    out = []
    methods = rows(game.md, "MethodDef")
    for rid, name in game.typedef_names.items():
        if bare(name) != type_name:
            continue
        for i in game.methods_of[rid - 1]:
            row = methods[i - 1]
            if str(row.Name) == method and row.Rva:
                head = game.pe.get_data(row.Rva, 8)
                out.append(head[0] >> 2 if head[0] & 3 == 2 else int.from_bytes(head[4:8], "little"))
    return out


def check(path):
    mod = Mod(path)
    report = Report()
    il2cpp = [r for r in mod.refs if r.startswith("Il2Cpp")]

    melon = framework = None
    attrs = []
    for ca in rows(mod.md, "CustomAttribute"):
        ctor = cidx(ca.Type)
        if not ctor or ctor[0] != MEMBERREF:
            continue
        owner, _, _ = mod.memberref(ctor[1])
        if owner is None:
            continue
        attrs.append((owner, ctor[1], cidx(ca.Parent), blob(ca.Value)))
        if owner[1] == "MelonLoader.MelonInfoAttribute":
            melon = [a for a in mod.attr_args(ctor[1], blob(ca.Value)) if isinstance(a, str)]
        elif owner[1] == "System.Runtime.Versioning.TargetFrameworkAttribute":
            framework = next((a for a in mod.attr_args(ctor[1], blob(ca.Value)) if isinstance(a, str)), None)

    title = " ".join(melon[:2]) if melon else mod.name
    if melon and len(melon) > 2:
        title += f" by {melon[2]}"
    print(f"{Path(path).name}: {title}" + (f" ({framework})" if framework else ""))
    if il2cpp:
        print(f"Quest (IL2CPP) build already: it references {', '.join(il2cpp[:3])}. Nothing to port.")
        return 0

    game_refs = [r for r in mod.refs if mod.side(r) == "game"]
    PC.extra = QUEST.extra = game_refs
    mod.scan()

    for ref in mod.refs:
        kind = mod.side(ref)
        if kind == "library":
            report.add("blocker", f"uses {ref}, which isn't part of the game: the Quest half needs a Quest "
                                  f"build of it too")
        elif kind == "game":
            qname = quest_name(ref)
            if QUEST.files and not QUEST.has(ref):
                report.add("blocker", f"uses {ref}, which the Quest build doesn't ship (no {qname})")
            else:
                report.add("info", f"reference {ref} is {qname} on Quest" if qname != ref
                           else f"reference {ref} keeps its name on Quest (a wrapper there)")
    if not QUEST.files:
        report.add("info", "no --quest-assemblies folder, so nothing was checked against the Quest build itself")
    if framework and "NETFramework" in framework:
        report.add("info", f"built for {framework}; Quest mods build for .NET 8 (net8.0)")
    marks = sorted(SERVER_MARKS & mod.strings)
    if marks:
        report.add("info", f"has server-only paths ({', '.join(marks)}): that half stays a PC mod on the "
                           f"server, only the player half needs porting")
    if mod.unreadable:
        report.add("info", f"{mod.unreadable} method bodies couldn't be read and weren't checked")

    def checkable(asm):
        return PC.has(asm) and QUEST.has(asm)

    unresolved = 0
    member_owner = {}
    for rid, row in enumerate(rows(mod.md, "MemberRef"), 1):
        owner, name, data = mod.memberref(rid)
        if owner is None or mod.side(owner[0]) != "game":
            continue
        where = mod.uses.get((MEMBERREF, rid), set())
        member_owner[rid] = owner
        is_field = bool(data) and data[0] == 0x06
        label = f"{pretty(owner[1])}.{re.sub(r'^(get_|set_|add_|remove_)', '', name)}"
        if name == ".ctor":
            label = f"new {pretty(owner[1])}"
        elif name.startswith(("add_", "remove_")):
            label += " (event)"
        if checkable(owner[0]):
            tname = bare(owner[1])
            on_pc = PC.lookup(owner[0], tname, member_keys(name, is_field))
            if on_pc:
                on_quest = QUEST.lookup(owner[0], tname, member_keys(name, is_field))
                if on_quest is None:
                    report.add("blocker", f"{pretty(owner[1])} doesn't exist in the Quest build", where)
                elif on_quest is False:
                    report.add("blocker", f"{label} doesn't exist in the Quest build", where)
            else:
                unresolved += 1
        try:
            sig = Sig(data)
            nodes = [sig.type()] if is_field else [n for part in [sig.method()] for n in [part[0], *part[1]]]
        except (IndexError, ValueError):
            continue
        if name == ".ctor" and nodes[1:] == [("prim", 0x1C), ("prim", 0x18)]:
            if where:
                report.add("rewrite", f"makes game delegate {pretty(owner[1])} from a mod method: on Quest build it "
                                      f"with DelegateSupport.ConvertDelegate", where)
            continue
        crossing = set()
        for node in nodes:
            for tok in walk(node, []):
                if tok in ("array", "object"):
                    crossing.add("arrays" if tok == "array" else "Object")
                    continue
                found = mod.type_of(tok)
                if found and CROSSING.match(found[1]):
                    crossing.add(pretty(found[1]).replace("System.", ""))
        if crossing and where:
            report.add("rewrite", f"{label} passes {', '.join(sorted(crossing))}: on Quest these are Il2CppSystem "
                                  f"types, convert at the call", where)

    for rid, row in enumerate(rows(mod.md, "TypeRef"), 1):
        asm, full = mod.typeref(rid)
        if mod.side(asm) != "game" or not checkable(asm):
            continue
        tname = bare(full)
        if PC.lookup(asm, tname, None) and QUEST.lookup(asm, tname, None) is None:
            where = set(mod.uses.get((TYPEREF, rid), set()))
            for mrid, owner in member_owner.items():
                if owner == (asm, full):
                    where |= mod.uses.get((MEMBERREF, mrid), set())
            report.add("blocker", f"{pretty(full)} doesn't exist in the Quest build", where)

    if unresolved:
        report.add("info", f"{unresolved} game members not found in the PC assemblies either; not judged")

    for owner, ctor, parent, value in attrs:
        if mod.side(owner[0]) != "game" or is_unity(owner[0]) or parent is None:
            continue
        if parent[0] == TYPEDEF:
            where = mod.typedef_names.get(parent[1])
        elif parent[0] == METHOD:
            where = mod.method_name(parent[1])
        else:
            where = "the assembly"
        report.add("blocker", f"[{pretty(owner[1]).removesuffix('Attribute')}] on mod code: the game finds these "
                              f"by scanning its own native code and will never see this one", [where])

    defs = rows(mod.md, "TypeDef")
    externals = {}
    for rid in range(1, len(defs) + 1):
        cur, steps = rid, 0
        while cur and steps < 20:
            ext = cidx(defs[cur - 1].Extends)
            if not ext or ext[0] != TYPEDEF:
                externals[rid] = mod.type_of(ext) if ext else None
                break
            cur, steps = ext[1], steps + 1
    for rid, base in externals.items():
        name = mod.typedef_names[rid]
        if base is None or name == "<Module>":
            continue
        methods = {str(rows(mod.md, "MethodDef")[i - 1].Name) for i in mod.methods_of[rid - 1]}
        if base[1] in MELON_BASES:
            hot = sorted(MELON_FRAME & methods)
            if hot:
                report.add("risk", f"{name} runs {', '.join(hot)} every frame: each call into the game crosses "
                                   f"into native code on Quest, keep it light", [name])
        elif mod.side(base[0]) == "game" and is_unity(base[0]):
            report.add("rewrite", f"{name} is a Unity component ({pretty(base[1])}): register it with "
                                  f"ClassInjector and give it an IntPtr constructor", [name])
            hot = sorted(UNITY_FRAME & methods)
            if hot:
                report.add("risk", f"{name} runs {', '.join(hot)} every frame on the headset", [name])
        elif mod.side(base[0]) == "game":
            report.add("blocker", f"{name} subclasses game class {pretty(base[1])}: the game's native code never "
                                  f"sees a managed subclass", [name])
    for row in rows(mod.md, "InterfaceImpl"):
        iface = mod.type_of(cidx(row.Interface))
        if iface and mod.side(iface[0]) == "game" and not is_unity(iface[0]):
            name = mod.typedef_names.get(row.Class.row_index, "?")
            report.add("blocker", f"{name} implements game interface {pretty(iface[1])}: the game's native code "
                                  f"can't call into it", [name])

    for rid, row in enumerate(rows(mod.md, "MethodDef"), 1):
        try:
            ret, params = Sig(blob(row.Signature)).method()
        except (IndexError, ValueError):
            continue
        toks = walk(ret, []) + [t for p in params for t in walk(p, [])]
        if any((mod.type_of(t) or ("", ""))[1] == "HarmonyLib.CodeInstruction" for t in toks):
            report.add("blocker", "transpiler: it edits the game's IL, and Quest has no IL, only native code",
                       [mod.method_name(rid)])

    class_patch, method_patch, kinds = defaultdict(dict), defaultdict(dict), {}
    for owner, ctor, parent, value in attrs:
        if parent is None:
            continue
        if owner[1] == "HarmonyLib.HarmonyPatch":
            target = class_patch if parent[0] == TYPEDEF else method_patch
            for key, val in patch_info(mod.attr_args(ctor, value)).items():
                target[parent[1]].setdefault(key, val)
        elif owner[1] in PATCH_ATTRS and parent[0] == METHOD:
            kinds[parent[1]] = PATCH_ATTRS[owner[1]]
    targets = []
    patch_classes = set(class_patch) | {mod.method_owner.get(m) for m in method_patch}
    for type_rid in patch_classes:
        if type_rid is None:
            continue
        for i in mod.methods_of[type_rid - 1]:
            mname = str(rows(mod.md, "MethodDef")[i - 1].Name)
            kind = kinds.get(i) or (mname if mname in ("Prefix", "Postfix", "Transpiler", "Finalizer") else None)
            if mname == "TargetMethod":
                report.add("info", "a patch picks its target in code (TargetMethod): check that target by hand",
                           [mod.method_name(i)])
            if not kind:
                continue
            info = dict(class_patch.get(type_rid, {}))
            info.update(method_patch.get(i, {}))
            if info.get("type"):
                targets.append((info["type"], info.get("name"), info.get("mt", 0), mod.method_name(i)))
    for where, owner, name, last_type, last_str, patches in mod.lookups:
        found = mod.type_of(last_type) if last_type else None
        if found and mod.side(found[0]) == "mod":
            continue
        label = f"{owner.split('.')[-1]}.{name}"
        if found and mod.side(found[0]) == "game":
            subject = f"{pretty(found[1])}" + (f", \"{last_str}\"" if last_str else "")
            if patches and owner == "HarmonyLib.AccessTools" and name in PATCH_LOOKUPS:
                mt = 1 if "Getter" in name else 2 if "Setter" in name else 3 if "Constructor" in name else 0
                targets.append(((found[0], bare(found[1])), last_str, mt, where))
            else:
                report.add("rewrite", f"{label}({subject}) reflects on a game type by name: the Quest wrapper "
                                      f"exposes fields as properties, so look-ups by name come back empty", [where])
        elif not found:
            subject = f"\"{last_str}\"" if last_str else "a type chosen at run time"
            report.add("rewrite", f"{label}({subject}): reflection by name, check it doesn't reach a game type",
                       [where])

    patched = set()
    for (asm, tname), name, mt, where in targets:
        if not asm or mod.side(asm) != "game":
            continue
        what = f"{tname}.{name}" if name else tname
        if mt in METHOD_TYPES:
            what += f" ({METHOD_TYPES[mt]})"
        patched.add(what)
        if checkable(asm) and name and PC.lookup(asm, tname, patch_keys(name, mt)):
            on_quest = QUEST.lookup(asm, tname, patch_keys(name, mt))
            if not on_quest:
                report.add("blocker", f"patches {what}, which doesn't exist in the Quest build", [where])
                continue
        game_type = PC.type(asm, tname) or {}
        if mt in (1, 2) or (name or "").startswith(("get_", "set_")):
            report.add("risk", f"patches {what}: property accessors are often inlined into callers on Quest, "
                               f"so the patch can miss them", [where])
        elif name and mt == 0:
            small = [s for s in il_sizes(asm, tname, name) if s <= SMALL_IL]
            if small:
                report.add("risk", f"patches {what}: {min(small)} bytes of IL, small enough that IL2CPP may inline "
                                   f"it and the patch miss those calls", [where])
        if game_type.get("generic") or name in game_type.get("gm", ()):
            report.add("risk", f"patches generic {what}: Quest has a native copy per type argument, the patch "
                               f"hits one at most", [where])
        if "<" in tname:
            report.add("risk", f"patches state machine {what}: its compiler-made name can differ in the Quest "
                               f"build", [where])
        if name in UNITY_FRAME:
            report.add("risk", f"patches {what}, which runs every frame", [where])

    if patched:
        report.add("info", f"patches {len(patched)} game methods: {', '.join(sorted(patched))}")

    def plural(n, word):
        return f"{n} {word}" + ("" if n == 1 else "s")

    blockers, rewrites, risks = (report.count(s) for s in SEVERITIES[:3])
    if blockers or rewrites or risks:
        print(f"PC build: needs a Quest port. {plural(blockers, 'blocker')}, {plural(rewrites, 'rewrite')}, "
              f"{plural(risks, 'risk')}.")
    else:
        print("PC build with nothing found that blocks a port: rebuild it against the Quest assemblies.")
    report.print()
    return 1 if blockers or rewrites or risks else 0


PC = Assemblies(None)
QUEST = Assemblies(None)
GAME_ASMS = set()


def main(argv=None):
    global PC, QUEST
    ap = argparse.ArgumentParser(description=ABOUT)
    ap.add_argument("dll", type=Path, help="the mod to check")
    ap.add_argument("--managed", type=Path, required=True,
                    help="the game's A Township Tale_Data\\Managed folder (PC)")
    ap.add_argument("--quest-assemblies", type=Path,
                    help="the Il2CppAssemblies folder LemonLoader generates for the Quest build")
    args = ap.parse_args(argv)
    if not args.managed.is_dir() or not (args.managed / "Root.Township.dll").is_file():
        print(f"no Root.Township.dll in {args.managed}: point --managed at the game's Managed folder",
              file=sys.stderr)
        return 2
    if args.quest_assemblies and not args.quest_assemblies.is_dir():
        print(f"no folder at {args.quest_assemblies}", file=sys.stderr)
        return 2
    PC = Assemblies(args.managed)
    QUEST = Assemblies(args.quest_assemblies, quest_name)
    GAME_ASMS.clear()
    GAME_ASMS.update(PC.files)
    try:
        return check(args.dll)
    except (OSError, ValueError, pefile.PEFormatError) as e:
        print(f"can't read {args.dll}: {e}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    sys.exit(main())
