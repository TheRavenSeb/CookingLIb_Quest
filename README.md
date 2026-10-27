# CookingLib

A library for A Township Tale mods. Other mods use it to add liquids, stew recipes and infusions (an item dropped into a liquid turns it into another liquid), to change the game's own recipes, ingredients, garnishes, stews, foods, liquids, cooking pots and what items turn into over a fire, and to hear when players cook, drink and eat. On its own it adds no content, but it keeps stews in pots across restarts.

## Install

Needs MelonLoader. Put `CookingLib.dll` in the `Mods` folder of the server. Players only need it when a mod they play with adds shared liquids, or the server turns on shared recipe pages:

| A mod adds | Server needs | Players need |
|---|---|---|
| Server only liquids, stew recipes, infusions, pot rules, roasting rules, stew stage changes | CookingLib and the mod | nothing, vanilla players can join |
| Shared liquids | CookingLib and the mod | CookingLib and the mod |
| Shared recipes | CookingLib and the mod | nothing while `SharedRecipePages` is off; with it on, CookingLib and the mod |
| Changes to game recipes, ingredients, garnishes, foods and liquids | CookingLib and the mod | nothing; with both they also see the new journal pages |

## For modders

Reference `CookingLib.dll` (keep `CookingLib.xml` next to it for tooltips) and register everything from `CookingLib.Main.Setup`:

```csharp
public override void OnInitializeMelon()
{
    CookingLib.Main.Setup += Setup;
}

static void Setup()
{
    Liquids.RegisterServerOnly(new LiquidSpec
    {
        Id = "you.ashwater",
        Look = "Water Liquid Visual Data",
        Color = new Color(0.55f, 0.53f, 0.5f),
        Effects = { new EffectSpec { Effect = "Heal", Strength = 0.5f } }
    });
}
```

A full example with every kind is in `Examples/CookingLibExample`. Its `cookingprobes` console module turns test setups on and off while the server runs (`cookingprobes list`, `cookingprobes on rules`, `cookingprobes off rules`). All are off at start except `water` and `roast`, which are read at boot: turn them on or off, then restart the server.

### Liquids

- `Liquids.RegisterServerOnly(spec)` makes a liquid only the server knows. Players receive it by value, so vanilla players see and drink it. It can be a stew base.
- `Liquids.RegisterShared(number, spec)` makes a real liquid under a number from 1 to 65535 that no other mod uses. The server and every player need CookingLib and your mod, and both sides must register it. It reaches nearby players whenever someone drinks or spills it.
- `Liquids.Fill(container, id, amount)` pours a liquid into an empty container or one holding the same liquid. Server only.
- `Liquids.Find(container.ContainedLiquid)` tells you which registered liquid a container holds, or null. Server only liquids match by value, so this still works after a restart.
- `Liquids.All` lists refused liquids too, so skip any with an `Error`. Read it after `Main.PostSetup`, once every mod has registered.
- Server only liquids add an internal entry named `cookinglib <id>` with a number above 65535 to the game's liquid list; skip them if you walk that list. A pot saves a server only base by that number, so it keeps it across a restart.

### Effects and how long they last

Every liquid, stew ingredient and recipe extra lists effects with a strength. The server applies the whole list each time a player sips, at that strength.

- Heal, Feed and Nourish add their strength at once.
- Speed, Damage, Damage Protection and Luminosity are buffs. Strength sets how strong a buff is, not how long it lasts. Each sip starts a new buff, and buffs on the same stat add up. Vanilla buffs last 15 minutes.
- `OverSeconds` gives Heal or Nourish out over that many seconds, 1 to 3600, instead of at once. Every other effect refuses it. Each sip or meal runs on its own, so two of them heal side by side. It works on liquids, foods and game liquids, and a server only liquid keeps it across a restart.
- A stew always applies its effects at once, so recipe extras and modifiers refuse `OverSeconds`, and a stew cooked on such a liquid gives it at once. `status` warns about such a recipe.
- Death ends every buff. A running buff survives logging out and restarts.

### Stew recipes

`StewRecipes.Register(spec)` adds a recipe. It borrows the look and name of a vanilla stew (`Look`, a vanilla recipe or stew prefab name), takes any registered or vanilla liquids as its base, and can change the stew's colour (`Tint`) or swap the finished stew for a liquid (`ResultLiquid`). These recipes never show up on recipe pages; shared recipes (below) can.

Like vanilla stews, extra effects scale with the stew's stage: about 0.3 at Raw, full at Cooked, nothing at Burnt. An `EffectModifiers` entry multiplies the stew's whole total for that effect and is scaled by the stage the same way.

### Shared recipes and pages

`StewRecipes.RegisterShared(number, spec)` adds a recipe that can also have a journal page. Call it from `Main.Setup` on the server and on players' machines, with the same number and spec on both. The number is 1 to 65535 and no other mod may use it; pick one from 64000 to 65535. It is yours forever in every world it runs in: unlocked pages and papers on the ground keep it, so never move a recipe to another number or give its number to another recipe.

The page only shows when the server turns it on in `UserData\MelonPreferences.cfg`, read at boot:

```
[CookingLib]
SharedRecipePages = true
```

- Off (the default): the recipe cooks like a server only one, and vanilla players can join.
- On: the page drops as loot and is listed in the Stewing Recipes book. Every player then needs CookingLib and your mod. A vanilla player who comes across the page sees a blank sheet and gets errors in their log; their stews still cook.
- Treat it as one way for a world. Turning it off later stops new pages, but papers already in the world keep the number.
- `cookinglib unlock <id>` (or `StewRecipes.UnlockPage(id)`) unlocks a page for everyone in the world.
- A page holds 4 slots and 3 effects (extras and modifiers together), and every effect needs a glyph. More is refused on both sides.
- An `Items` slot shows its first item's glyph; set `Glyph` to a game glyph from the dump, such as `Glyph_Potato_Full`, to show another. A `Category` slot always shows its category's glyph.
- If the server refuses the recipe once the world is up (a base liquid that doesn't resolve, say), the page stays and the recipe never cooks. `status` shows `shared as <number>` with the page state, and `HasPage` tells you in code.

### Infusions

`Infusions.Register(spec)` makes an item dropped into a liquid turn it into another liquid, for example coal into water makes black dye. One item converts the whole container.

Endless sources such as the Concoction Crate, and rivers and lakes, are skipped: nothing changes and the item stays. A crate that 0.1.0 already converted keeps its new liquid, because the content is saved. Replace it with a new crate to get a normal one back.

### Dyes

- `ColorsStews` on a liquid makes every stew cooked on it take the liquid's colour, at every stage. It replaces a recipe's `Tint` unless the recipe sets `IgnoresDyes`. A `ResultLiquid` is never recoloured.
- Players see the colour times the liquid's look, so give a dye a neutral look such as `Default Stew Raw Liquid Visual Data`. The Water look turns red into black.
- `CountsAsWater` lets the game's Water recipes match a pot of that liquid. A recipe that lists the liquid still wins, and the stew still gets that liquid's own effects and chunks.
- An infusion made in a pot with no ingredients changes the pot's base too, so the stew gets the new liquid's effects, chunks and colour. That pot only makes Water recipes when the new liquid sets `CountsAsWater`.
- A dyed stew and a plain one of the same ingredients don't top each other up.
- DyeingAPI v7 matches every custom liquid by colour, stews included, so a dyed stew dyes items by its new colour.

### Pot rules

`PotRules.Set(spec)` changes every cooking pot. Server only, so vanilla players need nothing. Leave a field null to keep the game's value:

| Field | Game | Allowed | What it does |
|---|---|---|---|
| `Ingredients` | 4 | 1 to 16 | how many ingredients lock a pot and pick its stew |
| `Garnishes` | 3 | 0 to 16 | how many garnishes a stew takes; one more floats instead of dissolving |
| `StewingTemperature` | 80 | above 23, below 100 | how hot the water and each ingredient must be to stew |
| `GlobalEffects` | Feed 3 | game effects, no `OverSeconds` | the effects every stew gets; an empty list gives none |
| `NoRecipeStew` | Non Recipe Raw Stew | a Raw stew prefab from the dump, not the default stew | the stew a pot locks on when its ingredients match no recipe |
| `RejectionForce` | 300 | 0 to 1000 | how hard a locked pot pushes a dropped ingredient out; at 0 no push is applied |

- Rules apply once the world is up, or at once after that. `PotRules.Remove(id)` gives the game's values back. Nothing is saved: the server applies them again after a restart, and removing the mod gives back vanilla pots.
- `NoRecipeStew` sets the look, name, colour, multipliers and cooking times of a stew with no recipe; its effects stay the plain sum of its ingredients. Pick a recipe's stew, such as `Pumpkin Recipe Raw Stew`, and a pot with the wrong ingredients looks like that stew and gives none of its buffs, on vanilla clients too; `status` warns. The default stew is refused, since a pot locked on it would take ingredients again after a reload. The name resolves once the world is up, so a wrong one refuses the rules then.
- `RejectionForce` is strength only: the push always goes up and the same way. At 0 no push is applied, so the ingredient should rest in the locked pot; untested.
- Every game recipe has 4 slots and a pot only matches recipes with exactly its count, so any other `Ingredients` stops every game recipe. Random recipe pages then skip recipes no pot can make, and `status` lists them. A recipe page has room for 4 ingredients.
- Water sits at the air temperature, about 23, so a pot set lower stews with no fire. Vanilla players hear the boiling sound start at 80 whatever the rule.
- Feed decides who can drink a stew: a player it would overfill is refused. `status` prints the most Feed a full pot's stew can carry and warns when that passes the game's own.
- `cookinglib pots` lists the pot definitions, the rules in force, which recipes pots can make, and every loaded pot with its stage, temperature, base, ingredients, garnishes and liquid effects.

### Pots after a restart

The game forgets a pot's stew when the server restarts or the pot's area unloads. With CookingLib on the server, every pot holding a stew picks up where it was, vanilla stews too. Players need nothing.

- The stew comes back as saved, with its recipe's colour, effects or result liquid, and keeps cooking to Cooked and Burnt.
- A pot that was locked stays locked: a new ingredient is pushed out instead of turning the stew into the non recipe stew.
- A pot short of its ingredients keeps the ones it has, so the rest still complete the recipe.
- Drinking a reloaded stew unlocks its ingredient pages again.
- `StewEvents.StageChanged` fires for its later stages. `Made` does not fire for the reload itself.
- `status` and `cookinglib pots` count the pots resumed, those with a recipe, bases missing and set to Water, and pots skipped.

### Game recipes and ingredients

`VanillaRecipes.Modify(spec)` changes one of the game's stew recipes in place, and `VanillaIngredients.Modify(spec)` changes what a game food adds to a stew. A field left null keeps the game's value.

```csharp
VanillaRecipes.Modify(new VanillaRecipeSpec
{
    Id = "you.berrystew",
    Recipe = "Berry Stew",
    ExtraEffects = new List<EffectSpec> { new EffectSpec { Effect = "Speed", Strength = 0.5f } },
    Tint = new Color(0.1f, 0.9f, 0.1f)
});
```

| Recipe field | What it changes |
|---|---|
| `ExtraEffects`, `EffectModifiers` | the stew's effects and the recipe page, written as given with no added x1 |
| `Tint`, `IgnoresDyes` | the stew's colour, as for your own recipes |
| `ResultLiquid` | a liquid the pot holds instead of the stew |
| `BaseLiquids` | the liquids the pot may hold for it, in place of Water |

| Ingredient field | What it changes |
|---|---|
| `Effects` | what the item adds to a stew, and its ingredient page. Eating it raw is unchanged |
| `Chunk` | the chunk it leaves in a stew, `""` for none |
| `Category` | the category recipes match it by, `""` for none |

- Call both from `Main.Setup` on the server and on players' machines. `ExtraEffects`, `EffectModifiers` and ingredient `Effects` change journal pages, so set them from Setup only. Vanilla players get the new stews but the game's own pages; players with CookingLib and your mod see the new pages. Everything else is server only and applies once the world is up.
- A page shows 3 effects. On a recipe page extra effects and modifiers share them, on an ingredient page every effect that is not 0 takes one. More is refused, because the server would fail to build that page.
- `VanillaRecipes.Disable(id, recipe)` turns a recipe off. No pot makes it, including pots reloaded after a restart, so its ingredients cook the next best recipe or the non recipe stew. Random recipe pages skip it; its pages already in books and saves stay.
- A `Category` move changes which recipes the item fits. `status` lists every recipe it touches and marks those where the order the ingredients go in decides which recipe wins.
- Feed may change here: stews reach players by value, so everyone gets the real numbers.
- `Revert(id)` gives back the game's values. A page reverted while the server runs can show twice in a book until the book reloads.
- Nothing is saved: without the mod the game's values come back at the next start. A stew already cooked keeps its values.

### Game garnishes

`VanillaGarnishes.Modify(spec)` changes one of the game's garnishes: Salt or a Garlic stage. A field left null keeps the game's value.

```csharp
VanillaGarnishes.Modify(new GarnishSpec
{
    Id = "you.salt",
    Item = "Salt",
    Multipliers = new List<EffectSpec> { new EffectSpec { Effect = "Protection", Strength = 2f } }
});
```

| Field | What it changes |
|---|---|
| `Effects` | what the garnish adds to a stew, and its ingredient page |
| `Multipliers` | what it multiplies, each 0 to 10: an effect already in the stew is scaled, one the stew lacks stays absent, and 0 removes it |
| `Chunk` | the chunk it leaves in a stew, `""` for none |

- Call it from `Main.Setup` on the server and on players' machines. `Effects` changes the garnish's journal page, so set it from Setup only; players with CookingLib and your mod see the new page, vanilla players the game's, and the stew gives the new effects to everyone. `Multipliers` and `Chunk` are server only and apply once the world is up. No page shows multipliers.
- Each garnish in a stew multiplies again, so two Salts at Protection x2 give x4. A recipe's own modifiers apply after.
- A garnish page only drops as loot; drinking a stew never unlocks it.
- Feed may change here: stews reach players by value.
- `Revert(id)` gives back the game's values. Nothing is saved.

### Stew stages

`VanillaStews.Modify(spec)` changes one stew prefab, one stage of one stew: how much its ingredients, its base liquid and its recipe count, and its colour. Server only, so vanilla players need nothing. Every stage is its own prefab; `cookinglib dump` lists the 54 under Stews, such as `Pumpkin Recipe Cooked Stew`.

```csharp
VanillaStews.Modify(new StewSpec
{
    Id = "you.pumpkin",
    Stew = "Pumpkin Recipe Cooked Stew",
    Ingredients = 1.5f,
    Tint = new Color(0.9f, 0.6f, 0.1f)
});
```

| Field | Game | Allowed | What it changes |
|---|---|---|---|
| `Ingredients` | 1 | 0 to 10 | multiplies what the ingredients add |
| `BaseLiquid` | 1 | 0 to 10 | multiplies what the base liquid adds |
| `Extras` | by stage | 0 to 10 | multiplies the recipe's extra effects, and its modifiers too |
| `Tint` | the stew's own | channels 0 to 1 | the stew's colour, under a recipe `Tint` and a liquid that colours stews |

- Call it from `Main.Setup` or later. It applies once the world is up, or at once after that, to stews already in pots and to pots reloading after a restart. A pot mid-stew gets the new numbers at its next ingredient, garnish or stage change; a stew already locked and cooked keeps its liquid until then.
- `Extras` on a recipe's stage also scales that recipe's modifiers, since the game uses the one number for both; `status` names the recipes. On the default and non recipe stews it changes nothing.
- A `Tint` on a stew look also colours every CookingLib recipe that borrows that look and sets no `Tint` of its own; `status` names them.
- The game's stew experience flag on each stage is read only: `dump` shows it and `StewEvents` carry it.
- `Revert(id)` gives back the game's values, on stews in pots too. Nothing is saved.

### Game foods and liquids

`Edibles.Register(spec)` changes what one of the game's foods gives when eaten, and `VanillaLiquids.Modify(spec)` changes what one of its liquids gives, such as a potion. `Effects` replaces the whole list.

```csharp
Edibles.Register(new EdibleSpec
{
    Id = "you.babuleg",
    Item = "Babu Leg Full Cooked",
    Effects = new List<EffectSpec>
    {
        new EffectSpec { Effect = "Heal", Strength = 0.3f, OverSeconds = 10 },
        new EffectSpec { Effect = "Nourish", Strength = 1.2f }
    }
});

VanillaLiquids.Modify(new VanillaLiquidSpec
{
    Id = "you.health",
    Liquid = "Health Potion",
    Effects = new List<EffectSpec> { new EffectSpec { Effect = "Heal", Strength = 3f } }
});
```

- Call both from `Main.Setup` on the server and on players' machines. Vanilla players get the new effects; players with CookingLib and your mod also see the new food pages.
- Feed stays the game's on foods and game liquids: a Feed entry is refused and the game's Feed is kept. Vanilla players judge how full they are with their own copy of the food or liquid, so a changed Feed would make them and the server disagree. For a more filling food, raise Nourish.
- Each stage is its own food, so `Babu Leg Full Ripe` and `Babu Leg Full Cooked` change separately. Only the game's own foods can change; anything else is refused. `Bites`, `BiteSeconds` and `LikeFood` are for a later CookingLib.
- A food page shows 3 effects and the game's Feed takes one of them. More is refused.
- An effect the food or liquid already has keeps its own `OverSeconds` unless you set one.
- A food change made after Setup applies at once, to foods already in the world too, and so does `Edibles.Revert(id)`. Books build their food pages when they load, so a book shows the old page until it reloads.
- `VanillaLiquids.Modify` applies right after `Main.PostSetup`; later calls are refused. Every bottle, river and stew base of that liquid uses the new effects. Effects only: its colour, look and chunks stay the game's. `VanillaLiquids.Revert(id)` undoes it.
- Water is refused unless the spec sets `AllowWater`, since it changes every stew and every water sip, and `status` says so. For every stew, `PotRules.GlobalEffects` is usually the better choice.
- Nothing is saved: without the mod the game's values come back at the next start.

### Roasting

`Roasting.Register(spec)` changes what one item turns into over a fire, and how fast it cooks and burns. Server only, so vanilla players need nothing.

```csharp
Roasting.Register(new RoastingSpec
{
    Id = "you.garlic",
    Item = "Garlic Full Ripe",
    CookedInto = "Pickaxe Head",
    CookSpeed = 4f
});
```

- Call it from `Main.Setup`. Rules apply once the world is up, or at once after that, items already in the world included.
- `CookedInto` and `BurntInto` take any prefab with a `Pickup` from the dump, or `Roasting.Nothing` to make the item vanish. Null keeps the game's. Stews and the item itself are refused.
- `CookSpeed` and `BurnSpeed` are 0 to 100: 4 is four times as fast, 0 never. An item the game never cooks or burns can't get either. Players see smoke and flames as usual, so a `BurnSpeed` 0 item shows flames and never burns.
- Each stage is its own item. Stews in pots are never touched, and a rule item in a pot holding liquid dissolves into the stew instead of turning into its result.
- The result lands where the item was. It stays in the hand when it is held the same way, so a two hand item that turns into a one hand item drops. It gets its prefab's default material.
- `Roasting.Roasted` fires for every roast, the game's own too, but never for a stew. It is a copy, and handlers run from the next frame. `LastHolder`, `UserId` and `UserName` name the last player who held the item, which is not proof they lit the fire.
- Nothing is saved. Items keep their progress across a restart and the rule applies again. If turning into a result fails, the rule turns off, the item roasts as in the game, and its `Error` and the log say so.
- `cookinglib dump` lists every item that roasts with its temperatures, seconds and results, and `status` counts each rule's roasts.

### Stew events

Server only, and read only:

- `StewEvents.Made` fires when a pot locks its stew. Usually that is the last ingredient, the 4th unless pot rules change the count, as a recipe (`Outcome` is `Recipe`) or the non recipe stew (`NoRecipe`). A stew that cooks or burns before then also locks the pot (`Unfinished`). It fires again with `IsRecheck` when another hot item lands in a locked pot in the same pass.
- `StewEvents.StageChanged` fires each time a stew cooks or burns (`Stage` is `Cooked` or `Burnt`), or burns away to nothing and the pot empties (`BurntAway`). When one change does both, `Made` comes first.

Each event is a copy taken when it happened. Handlers run on the main thread from the next frame, and a handler that throws is logged while the others still run. `Pot` may be gone by then, but `UserId` and `UserName` survive a player leaving.

- `Recipe` is a CookingLib id or a vanilla recipe name, `BaseLiquid` a CookingLib id or a vanilla liquid name, `StewLook` the stew prefab name. `Content` is the pot's liquid after the change, null for `BurntAway`.
- `Ingredients` are in the order they went in, `Garnishes` apart. `AddedBy` matches the last `AddedBy.Count` ingredients: the player who last held each item, or an empty entry (`Player` null, `UserId` 0) when nobody held it or that player left before the check. `Cooks` lists each player once.
- `CarriedOver` counts the first ingredients that came from a save, which have no `AddedBy` entry.
- `PotId` and `Position` are for this session's logs; pot ids change across restarts.
- `Cooks` and `IsAwardingExperienceStage` are the inputs of the game's unwired stew experience, see Cooking XP.
- `StewEvents.Error` says why the events are off. On players' machines it always says so.

### Cooking XP

The game never awards cooking experience: `ProgressionSettings.GainStewingExperience` has no caller, and the Cooking skill tree ships disabled with no skills, so even a wired call would give nothing. CookingLib adds no XP knob. The server's own `CookingXPX` in `ServerConfiguration.json` already multiplies cooking XP, should a skills mod ever wire it: `StewEvents` carry the inputs it takes (`Cooks` and `IsAwardingExperienceStage`; pass it a new list of at most 4 players, since it removes nulls from the list it gets), and `cookinglib dump` shows which stages carry the flag.

### Drink and eat events

Server only:

- `Drinks.Drank` fires each time liquid lands in a player's mouth and the game applies its effects: a pour, a sip with a cup at the lips, or a dunk. `Units` is how much landed, 1 per pour or sip and up to the mouth's free space for a dunk. `MouthSize` is how much the mouth holds. The game applies the effects once per drink, whatever `Units` is.
- `Drinks.Ate` fires when a player finishes eating something, with its `Item` and copies of the `Effects` the game applied. `Edible` is the CookingLib food change in place on it, or null. An abandoned bite fires nothing. It also fires for anything another mod makes edible.
- Each event is a copy taken when it happened. Handlers run on the main thread from the next frame, and a handler that throws is logged while the others still run. `UserId` and `UserName` come from the player's connection and are always set. `Player` is null once the player has left.
- `Drinks.CanDrink` is asked before liquid lands in a mouth. Return false to refuse it, the way the game refuses a drink on a full stomach: a pour spills on the ground and a cup held to the lips stays full. Vanilla players see no message and need nothing. It runs at once and often, every particle of a pour, so keep it quick and never move liquid in it. It is asked once per mouth, liquid and frame. A handler that throws allows.
- `Drinks.Error` says which events are off. On players' machines it always says so.
- `cookinglib status` counts drinks, meals, refused pours and tagged stews.

### What a liquid is

`Liquids.Identify(container.ContainedLiquid)`, or a `LiquidData`, says what a liquid is:

| `Kind` | `Name` |
|---|---|
| `Vanilla` | the game's liquid, such as `Water` |
| `Shared`, `ServerOnly` | the CookingLib id, and `Liquid` is the `LibLiquid` |
| `Stew` | the recipe's CookingLib id or vanilla name, null for a stew with no recipe, with `Recipe`, `Stage` and `Ingredients` |
| `Custom` | null |

- A stew taken out of its pot keeps its recipe, stage and ingredients only until it goes through a save: a restart, a chunk unload once everyone walks away, or a player logging out with the bowl. After that it is a `Stew` with no name, as the game itself forgets it. A stew still in its pot gets them back when the pot reloads.
- Answers are cached per liquid, so it is cheap to call often. `cookinglib dump` lists every name it can give.
- `Liquids.Find` still gives a CookingLib liquid straight away.

### For thirst mods

- `Liquids.Find(container.ContainedLiquid)` gives the CookingLib liquid in a container, or null for vanilla liquids and stews. Read `Spec.Thirst` from it and check `HasValue`: null leaves thirst to your mod.
- Look liquids up when a player drinks, or after `Main.PostSetup`. Before that, other mods may not have registered yet.
- Key liquids by `Id`. Server only liquids have no `Definition`, it is null.
- `Thirst` is a field, not an effect. Effect names come from `cookinglib dump`, and the short names `Speed`, `Protection` or `Glow` work too.
- `Liquids.Identify` tells the game's water (`Vanilla`, `Water`) from potions and stews by what they are, not by their look.
- `Drinks.Drank` tells you who drank what, and `Liquid.Thirst` is the same `Spec.Thirst`:

```csharp
Drinks.Drank += args =>
{
    float? thirst = args.Liquid?.Thirst;
    if (thirst.HasValue)
        MyThirst.Add(args.UserId, thirst.Value);
};
```

### Names

Specs use the game's own names. Run `cookinglib dump` in the server console to write every name to `UserData\CookingLib-dump.txt` and copy them from there. It also lists CookingLib's own liquids, each effect's settings with the strengths the game uses for it, the stews with their stages, multipliers and experience flags, the names `Liquids.Identify` gives, the pot definitions, the no recipe stew, the locked pot push and the drink gate, the endless sources infusions skip, every item that roasts, the glyphs, and the game's recipes, ingredients, garnishes, and changed foods and liquids as they are now with the mod that changed each. `cookinglib status` lists what registered, why anything was refused, the pot rules, roasting rules and shared recipe pages, the changes to game recipes, ingredients, garnishes, stews, foods and liquids with the game's value each replaced, the pots resumed after a restart, and whether the stew events, drink events and dyes are on.

A name that matches nothing is refused with up to 3 close names ("did you mean"), or with every name when there are 20 or fewer.

Effects also take these short names:

| Alias | Game effect (number) |
|---|---|
| Speed | Speed Indirect Effect (6764) |
| Damage | Damage Indirect effect (654) |
| Protection, Damage Protection | Damage Protection Indirect effect (64858) |
| Glow, Luminosity, Luminosity Indirect Effect | Luminosiy indirect effect (65294) |
| Heal | Heal (48456) |
| Feed | Feed (3280) |
| Nourish | Nourish (20046) |
| Poison | Poison over Time (29250) |
| Reveal | Reveal Effect (62720) |
| TeleportHome | Teleport Home (44252) |
| TeleportRespawn | Teleport Original Respawn (27214) |

A number is tried first, then the game's exact name, then an alias. An alias keeps its meaning in every later CookingLib. `status` and `dump` print the game's name next to any alias a spec used.

### Food values

`cookinglib foods` writes the game's live values to `UserData\CookingLib-foods.tsv`: liquids, eaten foods, stew ingredients, garnishes, recipes per stage, and stew stages. It replies with counts and how long the prefab walk took. `cookinglib foods liquids` (or `eaten`, `ingredients`, `garnishes`, `recipes`, `stages`, `all`) also prints that part. Where a CookingLib change touched a row, `was` holds the game's value and `by` the mod, and the printed part shows a changed effect as `0.3 (was 0.1)`. Garnish rows (`adds` and `times`) and stew stage rows fill them too. Changes by mods that don't use CookingLib don't show.

`cookinglib foods mix <stage> <item>, <item>, ... [+ <garnish>, ...]` runs the game's own stew math on Water, for example `cookinglib foods mix Cooked Carrot Full Ripe, Eggplant Full Ripe, Potato Full Ripe, Tomato Full Ripe + Salt`. The stage is `Raw`, `Cooked` or `Burnt`. It can't show a recipe's `ResultLiquid` or colour.

### Rules

- Ids are written as `author.thing`.
- Colours are 0 to 1 per channel.
- Effect strengths are minus 100 to 100. Players see at most 100, the server applies the full value. For scale, the game's own potions use about 2.
- `OverSeconds` is 1 to 3600, on Heal and Nourish in liquids, foods and game liquids. Stews refuse it.
- `Definition` is null for server only liquids; key them by `Id`.
- `Thirst` is only stored, for thirst mods such as HydrateOrDiedrate. It is minus 100 to 100, where 100 is a full thirst bar. Null leaves it to the thirst mod.
- A stew shows at most 3 extra effects. On a game recipe or a shared recipe, extra effects and modifiers share those 3.
- A pot matches recipes with exactly as many slots as its ingredient count, 4 unless pot rules change it. A recipe with another count still registers, and the log and `status` say no pot can normally make it.
- When two mods change the same thing, the first one wins and the same value agrees. A different value is refused whole, its `Error` names the mod that holds it, and `status` shows both.
- `BaseLiquids` and `InputLiquids` start as Water. Assign a new list to replace it.
- Registering and setting never throw. Check `Error` on what it returns, or read the log.

## Uninstalling

- Removing a mod whose liquid is a pot's stew base: while CookingLib runs, that pot carries on with Water as its base.
- Removing CookingLib itself leaves a pot holding a server only base with no base, stuck until it is emptied.
- Removing a mod that changes game recipes, ingredients, foods, liquids, pot rules or roasting gives back the game's values at the next start.
- Removing a mod with shared recipe pages turns its papers in the world into a Wooden Bag page at the next save, for good. Its pages stay unlocked, and putting the mod back brings back its book entry, not the papers.

## Building

Run `dotnet build`. The game path defaults to `C:\Games\Il2CppAlta\A Township Tale`, pass `-p:GamePath="your path"` to use another one. `python tools/check_style.py` checks the code style, and `dotnet run --project tests/CookingLib.Tests` runs the offline tests (the game must be installed, not running). The tests are not part of the release.

## Changes in 0.2.1

- New `VanillaGarnishes.Modify` changes what Salt and Garlic add to a stew, what they multiply and the chunk they leave, with `Revert`.
- New `VanillaStews.Modify` changes a stew stage's ingredient, base liquid and extras multipliers and its colour, on stews already in pots and reloading pots too, with `Revert`.
- `PotRules` gains `NoRecipeStew`, the stew a pot locks on with no recipe, and `RejectionForce`, the push on an ingredient dropped into a locked pot.
- `status` shows each garnish and stew change with the game's value it replaced, and warns about a no recipe stew that borrows a recipe's look and an `Extras` change that also scales a recipe's modifiers. Its worst-case Feed line compares against the game's own values even after ingredient changes.
- `dump` lists the nine garnishes, the stews' experience flags and the two new pot settings, and `foods` fills `was` and `by` on garnish and stew stage rows.
- The README says why there is no cooking XP knob.

## Changes in 0.2

- Breaking: `LibLiquid.Definition` is null for server only liquids. Key them by `Id`.
- Pots keep their stews across a restart or an area unloading, vanilla stews too: the colour, effects, recipe and lock come back, and a pot whose base liquid's mod was removed carries on with Water.
- Each server only liquid has its own pot number, so a pot keeps a server only base across a restart.
- New `PotRules`: how many ingredients lock a pot, how many garnishes a stew takes, the stewing temperature, and the effects every stew gets in place of Feed 3.
- New `VanillaRecipes.Modify` and `Disable`, `VanillaIngredients.Modify`, `Edibles.Register` and `VanillaLiquids.Modify` change the game's own recipes, ingredients, foods and liquids, each with `Revert`. Feed stays the game's on foods and game liquids.
- New `Roasting.Register` changes what an item turns into over a fire and how fast it cooks and burns, and `Roasting.Roasted` fires for every roast.
- New `StewRecipes.RegisterShared` adds a recipe with its own journal page, behind the server setting `SharedRecipePages` (off by default). `StewRecipes.UnlockPage` and `cookinglib unlock <id>` unlock a page for the world, and `SlotSpec.Glyph` picks a slot's glyph.
- New events: `StewEvents.Made` and `StageChanged`, `Drinks.Drank` and `Drinks.Ate`, and `Drinks.CanDrink` to refuse a drink.
- New `Liquids.Identify` says what a liquid is, and `LiquidInfo.Thirst` gives a CookingLib liquid's `Thirst` from a drink.
- `EffectSpec.OverSeconds` gives Heal or Nourish out over 1 to 3600 seconds on liquids, foods and game liquids.
- Dyes: `ColorsStews` colours every stew cooked on a liquid, `IgnoresDyes` keeps a recipe's own colour, and `CountsAsWater` lets the game's Water recipes match. An infusion in a pot with no ingredients changes the pot's base too.
- Effect names take short aliases such as Speed, Glow and Protection, and an unknown name suggests close names.
- When two mods change the same thing, the first wins, the same value agrees, and a different value is refused naming the mod that holds it.
- A recipe no pot can make is flagged from the pots' real ingredient count, and changes that would put more than 3 effects on a page are refused.
- New console commands: `cookinglib foods` (live values to `UserData\CookingLib-foods.tsv`, and `foods mix` for the game's stew math), `cookinglib pots` and `cookinglib unlock`. `status` and `dump` show everything new.
- The example covers every feature and adds a `cookingprobes` console module to switch test setups.

## Known issues

- 0.2 has not run on a live server yet. Everything new is built from the game's own code, so please test and report.
- If a stewing pot shows as skipped under `pot reloads` in `status`, it stays vanilla and loses its stew as before.
- Items resting in a locked pot after a reload just sit there, as in the game.
- `Drinks.Ate` and the stew events use Harmony features not yet tried on a live server. If one fails, `status` shows it off and the rest keeps working.
- How much a pour, a sip and a dunk give (`Units`) and the mouth size are untested.
- With a raised pot ingredient count, the first stew may cook before the last ingredient goes in.
- Vanilla players hear the boiling sound start at 80 whatever `StewingTemperature` says.
- Untested whether every item already in the world picks up a roasting rule at boot.
- A vanilla player who comes across a shared recipe page sees a blank sheet and gets errors in their log.
- `cookinglib unlock` can reply `unlock failed` while the world's pages are not loaded yet. Try again a little later.
- The replies of `cookinglib foods` and `cookinglib pots` can be long for some console tools. The TSV and dump files hold everything.
- Books show an old food, ingredient, garnish or recipe page until they reload, and a page reverted while the server runs can show twice.
- A stew locked and cooked before a stew stage change keeps its liquid until its next ingredient, garnish or stage change.
- What an ingredient dropped into a locked pot does at `RejectionForce` 0, and whether a vanilla client sees a changed push, are untested.

## Please test and report

0.2 has only been checked against the game's code. Run the example mod (`Examples/CookingLibExample`) and tell us what you see, with the log lines it prints.

**Pots after a restart**

- Cook a recipe stew, leave it at Raw, `save now`, restart: the same colour and effects, it cooks on to Cooked, and `status` shows it under `pot reloads` as resumed with a recipe.
- Put 2 ingredients in a pot, walk away until the area unloads, come back and add 2 more: the recipe matches.
- After a reload, walk up to a locked pot and drop an ingredient in: the stew shows once and the ingredient is pushed out.

**Dyes**

- Drop Coal into a pot of water with no ingredients, then cook a stew in it to Burnt: black at every stage, while a plain Water stew looks normal.
- With DyeingAPI v7 on the server, dip an item it can dye into a black stew: tell us which colour it takes.

**Drinking and eating**

- Pour Water into your mouth, sip from a cup, and dunk a bowl in a river: send the `drank:` lines.
- Finish a food, and leave another after one bite: one `ate:` line, none for the one you left.
- `cookingprobes on nowater`: a pour of Water spills and a cup at the lips stays full, on a vanilla client too.
- With HydrateOrDiedrate on the same server, thirst still works.

**Stew events**

- Two players add ingredients to one Berry Stew: send the `stew made` and `stew stage changed` lines.
- One ingredient alone in a hot pot: it cooks, locks the pot (`Unfinished`), and the next item is pushed out.

**Pot rules**

- `cookingprobes on rules`: a 3 ingredient stew locks and a second garnish floats. `cookingprobes off rules` gives vanilla pots back.
- `cookingprobes on big`: does the first stew cook before the 8th ingredient goes in?

**Game recipes, foods and liquids**

- On a vanilla client, cook Berry Stew: green, with Speed. A modded client also sees the new journal page.
- Eat Babu Leg Full Cooked on a vanilla client: it heals over 10 seconds, with no server error.
- Drink a Health Potion bottled before the server started: it gives the example's Heal 3.

**Roasting**

- Hold Garlic Full Ripe over a campfire: it turns into a Pickaxe Head at about four times the usual speed, with a `roasted:` line. Tell us which metal it is.
- Warm a rule item partway, `save now`, restart: it carries on at the rule's speed. Send the `roasting: applied to N live items` boot line.

**Shared recipe pages**

- Set `SharedRecipePages = true`, restart, run `cookinglib unlock example.hearthstew` and open a Stewing Recipes book on a modded client: the hearth stew page is last and draws its glyphs.
- Tell us what a vanilla player sees on that page, and that they stay connected.

**Food values**

- `cookinglib foods all` on a fresh boot: send the reply, how long it took, and whether the whole reply reached your console tool.

**Garnishes and stew stages**

- `cookinglib foods stages` and `cookinglib dump` on any server: send the stages part and the Stews section, with the multipliers, tints and xp flags of all 54 stews.
- Cook 4 cooked eggplant halves with 1 Salt, then run `cookinglib foods mix Cooked` with the same four and `+ Salt`: Protection is doubled by the example's Salt change, and the `drank:` line agrees.
- Spawn Garlic Full Cooked's ingredient page: a modded client reads Nourish 0.5, a vanilla client the game's 0.1, and a stew with that garnish gives 0.5 to both.
- Lock a pot on the non recipe stew with 4 unmatched ingredients, `cookingprobes on stew`, then drop Salt in: the stew turns green and `cookinglib pots` shows its effects at x1.5. `save now` and restart: still green, and `status` counts the pot's stews under reloading pots.
- `cookingprobes on norecipe`, then 4 unmatched ingredients: the pot locks on a pumpkin looking stew with no pumpkin buffs, on a vanilla client too. Restart: the pot resumes locked with no recipe.
- `cookingprobes on push0`, drop a carrot into a locked pot: it rests in the pot. `cookingprobes off push0`, `cookingprobes on push1000`, drop another: it flies. Tell us what a vanilla client sees both times.

## Reporting problems

Open an issue on GitHub with:

- the output of `cookinglib status`
- `UserData\CookingLib-dump.txt`, written by `cookinglib dump`
- `UserData\CookingLib-foods.tsv`, written by `cookinglib foods`
- the server log lines with `[CookingLib]`, from `MelonLoader\Latest.log`
- what the player did, step by step, and what they saw

## License

MIT
