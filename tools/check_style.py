import ast
import io
import pathlib
import sys
import tokenize

ROOT = pathlib.Path(__file__).resolve().parent.parent
DASHES = (chr(0x2013), chr(0x2014))
MARKUP_COMMENT = "<" + "!--"
SKIP = {"bin", "obj", ".git", ".vs", ".vscode"}
TEXT = {".cs", ".csproj", ".props", ".md", ".txt", ".py"}


def files():
    for path in sorted(ROOT.rglob("*")):
        if path.is_file() and path.suffix in TEXT and not SKIP.intersection(path.relative_to(ROOT).parts):
            yield path


def csharp_comments(text):
    i, line, n = 0, 1, len(text)
    while i < n:
        c = text[i]
        if c == "\n":
            line += 1
        elif text.startswith("//", i):
            end = text.find("\n", i)
            end = n if end < 0 else end
            yield line, text[i:end]
            i = end
            continue
        elif text.startswith("/*", i):
            yield line, "/*"
            end = text.find("*/", i + 2)
            end = n if end < 0 else end + 2
            line += text.count("\n", i, end)
            i = end
            continue
        elif c in "\"'":
            verbatim = i > 0 and text[i - 1] == "@"
            i += 1
            while i < n:
                if text[i] == "\n":
                    line += 1
                if verbatim and text.startswith('""', i) or not verbatim and text[i] == "\\":
                    i += 2
                    continue
                if text[i] == c:
                    break
                i += 1
        i += 1


def check(path):
    rel = path.relative_to(ROOT).as_posix()
    text = path.read_text(encoding="utf-8-sig")
    for number, line in enumerate(text.splitlines(), 1):
        if any(dash in line for dash in DASHES):
            yield f"{rel}:{number}: em or en dash"
        if MARKUP_COMMENT in line:
            yield f"{rel}:{number}: comment"
    if path.suffix == ".cs":
        docs_allowed = rel.startswith("CookingLib/Api/")
        for number, comment in csharp_comments(text):
            if not (docs_allowed and comment.startswith("///")):
                yield f"{rel}:{number}: comment"
    if path.suffix == ".py":
        for token in tokenize.generate_tokens(io.StringIO(text).readline):
            if token.type == tokenize.COMMENT:
                yield f"{rel}:{token.start[0]}: comment"
        for node in ast.walk(ast.parse(text)):
            if isinstance(node, (ast.Module, ast.FunctionDef, ast.AsyncFunctionDef, ast.ClassDef)) and ast.get_docstring(node) is not None:
                yield f"{rel}:{getattr(node, 'lineno', 1)}: docstring"


def main():
    problems = [problem for path in files() for problem in check(path)]
    for problem in problems:
        print(problem)
    print(f"{len(problems)} problem(s)" if problems else "style ok")
    return 1 if problems else 0


if __name__ == "__main__":
    sys.exit(main())
