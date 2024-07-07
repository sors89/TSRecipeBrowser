using Terraria;
using Terraria.Map;
using Terraria.ID;
using Terraria.GameContent.ItemDropRules;
using TerrariaApi.Server;
using TShockAPI;

namespace RecipeBrowser
{
    [ApiVersion(2, 1)]
    public class RecipeBrowser : TerrariaPlugin
    {
        public override string Author => "Sors";

        public override string Description => "Allow players browsing an item's recipe and drop info.";

        public override string Name => "Recipe Browser";

        public override Version Version => new(1, 0, 0);

        const int IconicWoodID = 9;
        const int IconicSandID = 169;
        const int IconicIronBarID = 22;
        const int IconicFragmentID = 3458;
        const int IconicPressurePlateID = 852;
        public Dictionary<int, List<Recipe>> Recipes { get; set; } = new Dictionary<int, List<Recipe>>();
        public Dictionary<int, List<KeyValuePair<string, float>>> NPCLootsTable { get; set; } = new Dictionary<int, List<KeyValuePair<string, float>>>();
        public Dictionary<int, string> ItemDropConditions { get; set; } = new Dictionary<int, string>();
        public RecipeBrowser(Main game) : base(game)
        {
            Order = 0;
        }

        public override void Initialize()
        {
            MapHelper.Initialize();
            BuildMapAtlas.Initialize();

            ServerApi.Hooks.GamePostInitialize.Register(this, OnGamePostInit);

            Commands.ChatCommands.Add(new Command("recipebrowser.browse", RecBrowser, "recipe", "rec")
            {
                AllowServer = false,
                HelpText = "Browse an item's recipe",
            });

            Commands.ChatCommands.Add(new Command("recipebrowser.browse", ItemDropSourceBrowser, "dropsrc", "ds")
            {
                AllowServer = false,
                HelpText = "Browse an item's drop source and drop rate",
            });
        }

        private void OnGamePostInit(EventArgs args)
        {
            foreach (Recipe recipe in Main.recipe)
            {
                int targetItemID = recipe.createItem.type;
                if (!Recipes.ContainsKey(targetItemID))
                    Recipes.Add(targetItemID, new List<Recipe>());
                Recipes[targetItemID].Add(recipe);
            }

            for (int i = -65; i < NPCID.Count; i++)
            {
                List<IItemDropRule> dropRules = Main.ItemDropsDB.GetRulesForNPCID(i); //also include global items, we will deal with this later

                List<DropRateInfo> drops = new List<DropRateInfo>();
                DropRateInfoChainFeed ratesInfo = new DropRateInfoChainFeed(1f);
                foreach (IItemDropRule item in dropRules)
                    item.ReportDroprates(drops, ratesInfo);

                foreach (DropRateInfo dropRateInfo in drops)
                {
                    if (!NPCLootsTable.ContainsKey(dropRateInfo.itemId))
                        NPCLootsTable.Add(dropRateInfo.itemId, new List<KeyValuePair<string, float>>());
                    NPCLootsTable[dropRateInfo.itemId].Add(new KeyValuePair<string, float>(Lang.GetNPCNameValue(i), dropRateInfo.dropRate));
                }
            }

            /*
            we are dealing with global items, global items always have the same drop source i.e Jungle Key will always be dropped by
            hardmode jungle enemies so that we just need to get the first condition instead of the whole list which will take a lot of memomy.
            */
            foreach (IItemDropRule itemDropRule in Main.ItemDropsDB._globalEntries)
            {
                List<DropRateInfo> drops = new List<DropRateInfo>();
                DropRateInfoChainFeed ratesInfo = new DropRateInfoChainFeed(1f);

                itemDropRule.ReportDroprates(drops, ratesInfo);

                foreach (DropRateInfo dropRateInfo in drops)
                {
                    if (!ItemDropConditions.ContainsKey(dropRateInfo.itemId))
                        ItemDropConditions.Add(dropRateInfo.itemId, dropRateInfo.conditions
                            .Select(i => i.GetConditionDescription())
                            .First());
                }
            }
        }

        private void RecBrowser(CommandArgs args)
        {
            if (!args.Player.HasPermission("recipebrowser.browse"))
            {
                args.Player.SendErrorMessage("You do not have permission to access that command");
                return;
            }

            List<string> parameters = args.Parameters;
            if (parameters.Count < 1 || parameters.Count > 1)
            {
                args.Player.SendErrorMessage($"Invalid syntax. Proper syntax: {TShock.Config.Settings.CommandSpecifier}rec <item name/id>");
                return;
            }

            List<Item> matchedItems = TShock.Utils.GetItemByIdOrName(parameters[0]);
            Item targetItem = new Item();
            if (matchedItems.Count == 0)
            {
                args.Player.SendErrorMessage("Invalid item type!");
                return;
            }
            else if (matchedItems.Count > 1)
            {
                args.Player.SendMultipleMatchError(matchedItems.Select(i => $"{i.Name}({i.netID})"));
                return;
            }
            else
            {
                targetItem = matchedItems[0];
            }

            if (Recipes.ContainsKey(targetItem.type))
            {
                args.Player.SendInfoMessage($"Found {Recipes[targetItem.type].Count} way(s) to craft [c/ff6600: {targetItem.Name} (ID: {targetItem.netID})]:");
                foreach (Recipe recProperty in Recipes[targetItem.type])
                {
                    List<string> ItemArray = new List<string>();
                    string fancyItemMsg = "";
                    fancyItemMsg = $"[[i/s{recProperty.createItem.stack}:{recProperty.createItem.type}]] [c/ffff00: ←] ";
                    foreach (Item item in recProperty.requiredItem)
                    {
                        if (item.netID != 0)
                        {
                            //there are some items that allow players to use any type of its ingredient to craft it
                            if ((recProperty.anyWood && item.type == IconicWoodID) ||
                                (recProperty.anySand && item.type == IconicSandID) ||
                                (recProperty.anyIronBar && item.type == IconicIronBarID) ||
                                (recProperty.anyFragment && item.type == IconicFragmentID) ||
                                (recProperty.anyPressurePlate && item.type == IconicPressurePlateID))
                                ItemArray.Add($"[Any [i/s{item.stack}:{item.type}]]");
                            else
                                ItemArray.Add($"[[i/s{item.stack}:{item.type}]]");
                        }

                    }
                    fancyItemMsg += string.Join(" [c/ffffff: ✛] ", ItemArray);
                    args.Player.SendSuccessMessage(fancyItemMsg);

                    List<string> TileArray = new List<string>();
                    string fancyTileMsg = "";
                    foreach (int id in recProperty.requiredTile)
                    {
                        if (id != -1)
                        {
                            string tileName = Lang._mapLegendCache[MapHelper.TileToLookup(id, Utils.GetRequiredTileStyle(id, recProperty.crimson))].ToString();
                            TileArray.Add($"[c/ff3333: {tileName}]");
                        }
                    }
                    if (recProperty.needEverythingSeed)
                        TileArray.Add("[c/ff3333: Everything World]");
                    if (recProperty.needGraveyardBiome)
                        TileArray.Add("[c/ff3333: Graveyard Biome]");
                    if (recProperty.needHoney)
                        TileArray.Add("[c/ff3333: Honey]");
                    if (recProperty.needLava)
                        TileArray.Add("[c/ff3333: Lava]");
                    if (recProperty.needSnowBiome)
                        TileArray.Add("[c/ff3333: Snow Biome]");
                    if (recProperty.needWater)
                        TileArray.Add("[c/ff3333: Water]");
                    fancyTileMsg = string.Join(" [c/ffffff: &] ", TileArray);

                    if (recProperty.alchemy)
                        fancyTileMsg += "[c/ff3333: or Alchemy Table]";
                    if (fancyTileMsg == "")
                        fancyTileMsg = "[c/ff3333: By Hand]";
                    args.Player.SendSuccessMessage("Crafting Station: " + $"{fancyTileMsg}");
                }
            }
            else
                args.Player.SendErrorMessage($"[c/ff6600: {targetItem.Name} (ID: {targetItem.netID})] does not have a recipe");

            if (NPCLootsTable.ContainsKey(targetItem.type))
                args.Player.SendInfoMessage($"Type [c/0066cc: {TShock.Config.Settings.CommandSpecifier}ds {targetItem.netID}] to view {NPCLootsTable[targetItem.type].Count} npc(s) that drop following item: [c/ff6600: {targetItem.Name} (ID: {targetItem.netID})]:");
        }
        private void ItemDropSourceBrowser(CommandArgs args)
        {
            if (!args.Player.HasPermission("recipebrowser.browse"))
            {
                args.Player.SendErrorMessage("You do not have permission to access that command");
                return;
            }

            List<string> parameters = args.Parameters;
            if (parameters.Count < 1 || parameters.Count > 2)
            {
                args.Player.SendErrorMessage($"Invalid syntax. Proper syntax: {TShock.Config.Settings.CommandSpecifier}ds <item name/id> <page number>");
                return;
            }

            List<Item> matchedItems = TShock.Utils.GetItemByIdOrName(parameters[0]);
            Item targetItem = new Item();
            if (matchedItems.Count == 0)
            {
                args.Player.SendErrorMessage("Invalid item type!");
                return;
            }
            else if (matchedItems.Count > 1)
            {
                args.Player.SendMultipleMatchError(matchedItems.Select(i => $"{i.Name}({i.netID})"));
                return;
            }
            else
            {
                targetItem = matchedItems[0];
            }

            int pageNumber;
            if (NPCLootsTable.ContainsKey(targetItem.netID))
            {
                if (!PaginationTools.TryParsePageNumber(args.Parameters, 1, args.Player, out pageNumber))
                    return;
                IEnumerable<string> itemDropSrc = NPCLootsTable[targetItem.netID].Select(i => $"{i.Key} ({Math.Round(i.Value * 100f, 2)}%)");
                PaginationTools.SendPage(args.Player, pageNumber, PaginationTools.BuildLinesFromTerms(itemDropSrc),
                    new PaginationTools.Settings
                    {
                        HeaderFormat = $"[c/ff6600: {targetItem.Name}] Drop Source ({{0}}/{{1}}): ",
                        FooterFormat = $"Type {TShock.Config.Settings.CommandSpecifier}ds {targetItem.netID} {{0}} for more.",
                        HeaderTextColor = Microsoft.Xna.Framework.Color.LimeGreen,
                        FooterTextColor = Microsoft.Xna.Framework.Color.LimeGreen,
                    });

                //add extra information about condition to drop for global items to make it less confusing for players
                if (ItemDropConditions.ContainsKey(targetItem.netID))
                    args.Player.SendInfoMessage($"Condition: [c/66ff33: {ItemDropConditions[targetItem.netID]}]");
            }
            else
                args.Player.SendErrorMessage($"Can not find any npcs that drop [c/ff6600: {targetItem.Name} (ID: {targetItem.netID})]");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Recipes.Clear(); NPCLootsTable.Clear(); ItemDropConditions.Clear();
                ServerApi.Hooks.GamePostInitialize.Deregister(this, OnGamePostInit);
            }
            base.Dispose(disposing);
        }
    }
}
