using System.Collections.Generic;
using System.Linq;
using System;
using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("Stack Size Controller", "Canopy Sheep", "2.0.4", ResourceId = 2320)]
    [Description("Allows you to set the max stack size of every item.")]
    public class StackSizeController : RustPlugin
    {
        #region Data

        private bool pluginLoaded = false;
        Items items;
        class Items
        {
            public Dictionary<string, int> itemlist = new Dictionary<string, int>();
        }

        private bool LoadData()
        {
            var itemsdatafile = Interface.Oxide.DataFileSystem.GetFile("StackSizeController");
            try
            {
                items = itemsdatafile.ReadObject<Items>();
                return true;
            }
            catch (Exception ex)
            {
                PrintWarning("Error: Data file is corrupt. Debug info: " + ex.Message);
                return false;
            }
        }

        private void UpdateItems()
        {
            var gameitemList = ItemManager.itemList;
            List<string> itemCategories = new List<string>();
            int stacksize;

            foreach (var item in gameitemList)
            {
                if (!itemCategories.Contains(item.category.ToString()))
                {
                    if (!(configData.Settings.CategoryDefaultStack.ContainsKey(item.category.ToString())))
                    {
                        configData.Settings.CategoryDefaultStack[item.category.ToString()] = configData.Settings.NewCategoryDefaultSetting;
                        Puts("Added item category: '" + item.category.ToString() + "' to the config.");
                    }
                    itemCategories.Add(item.category.ToString());
                }

                if (!(items.itemlist.ContainsKey(item.displayName.english)))
                {
                    stacksize = DetermineStack(item);
                    items.itemlist.Add(item.displayName.english, stacksize);
                }
            }

            List<string> KeysToRemove = new List<string>();

            foreach (KeyValuePair<string ,int> category in configData.Settings.CategoryDefaultStack)
            {
                if (!itemCategories.Contains(category.Key)) { KeysToRemove.Add(category.Key); }
            }

            if (KeysToRemove.Count > 0)
            {
                Puts("Cleaning config categories...");
                foreach (string Key in KeysToRemove)
                {
                    configData.Settings.CategoryDefaultStack.Remove(Key);
                }
            }

            SaveConfig();

            KeysToRemove = new List<string>();
            bool foundItem = false;

            foreach (KeyValuePair<string, int> item in items.itemlist)
            {
                foreach (var itemingamelist in gameitemList)
                {
                    if (itemingamelist.displayName.english == item.Key)
                    {
                        foundItem = true;
                        break;
                    }
                }
                if (!(foundItem)) { KeysToRemove.Add(item.Key); }
                foundItem = false;
            }

            if (KeysToRemove.Count > 0)
            {
                Puts("Cleaning data file...");
                foreach (string key in KeysToRemove)
                {
                    items.itemlist.Remove(key);
                }
            }

            SaveData();
            LoadStackSizes();
        }

        private int DetermineStack(ItemDefinition item)
        {
            if (item.condition.enabled && item.condition.max > 0 && (!configData.Settings.StackHealthItems))
            {
                return 1;
            }
            else
            {
                if (configData.Settings.DefaultStack != 0 && (!configData.Settings.CategoryDefaultStack.ContainsKey(item.category.ToString())))
                {
                    return configData.Settings.DefaultStack;
                }
                else if (configData.Settings.CategoryDefaultStack.ContainsKey(item.category.ToString()) && configData.Settings.CategoryDefaultStack[item.category.ToString()] != 0)
                {
                    return configData.Settings.CategoryDefaultStack[item.category.ToString()];
                }
                else if (configData.Settings.DefaultStack != 0 && configData.Settings.CategoryDefaultStack[item.category.ToString()] == 0)
                {
                    return configData.Settings.DefaultStack;
                }
                else
                {
                    return item.stackable;
                }
            }
        }

        private void LoadStackSizes()
        {
            var gameitemList = ItemManager.itemList;

            foreach (var item in gameitemList)
            {
                item.stackable = items.itemlist[item.displayName.english];
            }
        }

        private void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject("StackSizeController", items);
        }

        #endregion

        #region Config

        ConfigData configData;
        class ConfigData
        {
            public SettingsData Settings { get; set; }
        }

        class SettingsData
        {
            public int DefaultStack { get; set; }
            public int NewCategoryDefaultSetting { get; set; }
            public bool StackHealthItems { get; set; }
            public Dictionary<string, int> CategoryDefaultStack { get; set; }
        }

        private void TryConfig()
        {
            try
            {
                configData = Config.ReadObject<ConfigData>();
            }
            catch (Exception ex)
            {
                PrintWarning("Corrupt config detected, debug: " + ex.Message);
                LoadDefaultConfig();
            }
        }

        protected override void LoadDefaultConfig()
        {
            Puts("Generating a new config file...");

            Config.WriteObject(new ConfigData
            {
                Settings = new SettingsData
                {
                    DefaultStack = 0,
                    NewCategoryDefaultSetting = 0,
                    StackHealthItems = true,
                    CategoryDefaultStack = new Dictionary<string, int>()
                    {
                        { "Ammunition", 0 },
                        { "Weapon", 0 },
                    },
                },
            }, true);
        }

        private void SaveConfig()
        {
            Config.WriteObject(configData);
        }
        #endregion

        #region Hooks
        private void OnServerInitialized()
        {
            TryConfig();
            pluginLoaded = LoadData();

            if (pluginLoaded) 
            {
                if (!configData.Settings.StackHealthItems) { Unsubscribe(); }
                UpdateItems(); 
            }
            else { Puts("Stack Sizes could not be changed due to a corrupt data file."); }

            permission.RegisterPermission("stacksizecontroller.canChangeStackSize", this);
        }

        private bool hasPermission(BasePlayer player, string perm)
        {
            if (player.net.connection.authLevel > 1)
            {
                return true;
            }
            return permission.UserHasPermission(player.userID.ToString(), perm);
        }
        
        private object CanStackItem(Item item, Item targetItem)
        {
            if (item.info.shortname != targetItem.info.shortname) { return null; }
            if (item.contents != targetItem.contents) { return false; }

            FlameThrower flamethrower = item.GetHeldEntity() as FlameThrower;
            if (flamethrower != null)
            {
                if (flamethrower.ammo != (targetItem.GetHeldEntity() as FlameThrower).ammo) { return false; }
            }
			return null;
        }

        private void Unsubscribe()
        {
            Unsubscribe(nameof(CanStackItem));
        }

        #endregion

        #region Commands
        [ChatCommand("stack")]
        private void StackCommand(BasePlayer player, string command, string[] args)
        {
            if (!hasPermission(player, "stacksizecontroller.canChangeStackSize"))
            {
                SendReply(player, "You don't have permission to use this command.");
                return;
            }

            if (!pluginLoaded)
            {
                SendReply(player, "StackSizeController has encountered an error while trying to read the data file. Please contact your server administrator to fix the issue.");
                return;
            }

            if (args.Length < 2)
            {
                SendReply(player, "Syntax Error: Requires 2 arguments. Syntax Example: /stack ammo.rocket.hv 64 (Use shortname)");
                return;
            }

            int stackAmount = 0;

            List<ItemDefinition> gameitems = ItemManager.itemList.FindAll(x => x.shortname.Equals(args[0]));

            if (gameitems.Count == 0)
            {
                SendReply(player, "Syntax Error: That is an incorrect item name. Please use a valid shortname.");
                return;
            }

            string replymessage = "";
            switch (args[1].ToLower())
            {
                case "default":
                {
                    stackAmount = DetermineStack(gameitems[0]);
                    replymessage = "Updated Stack Size for " + gameitems[0].displayName.english + " (" + gameitems[0].shortname + ") to " + stackAmount + " (Default value based on config).";
                    break;
                }
                default:
                {
                    if (int.TryParse(args[1], out stackAmount) == false)
                    {
                        SendReply(player, "Syntax Error: Stack Amount is not a number. Syntax Example: /stack ammo.rocket.hv 64 (Use shortname)");
                        return;
                    }
                    replymessage = "Updated Stack Size for " + gameitems[0].displayName.english + " (" + gameitems[0].shortname + ") to " + stackAmount + ".";
                    break;
                }
            }

            if (gameitems[0].condition.enabled && gameitems[0].condition.max > 0)
            {
                if (!(configData.Settings.StackHealthItems))
                {
                    SendReply(player, "Error: Stacking health items is disabled in the config.");
                    return;
                }
            }

            items.itemlist[gameitems[0].displayName.english] = Convert.ToInt32(stackAmount);
                
            gameitems[0].stackable = Convert.ToInt32(stackAmount);

            SaveData();

            SendReply(player, replymessage);
        }

        [ChatCommand("stackall")]
        private void StackAllCommand(BasePlayer player, string command, string[] args)
        {
            if (!hasPermission(player, "stacksizecontroller.canChangeStackSize"))
            {
                SendReply(player, "You don't have permission to use this command.");
                return;
            }

            if (!pluginLoaded)
            {
                SendReply(player, "StackSizeController has encountered an error while trying to read the data file. Please contact your server administrator to fix the issue.");
                return;
            }

            if (args.Length < 1)
            {
                SendReply(player, "Syntax Error: Requires 1 argument. Syntax Example: /stackall 65000");
                return;
            }

            int stackAmount = 0;
            string replymessage = "";

            var itemList = ItemManager.itemList;

            foreach (var gameitem in itemList)
            {
                switch (args[0].ToLower())
                {
                    case "default":
                    {
                        stackAmount = DetermineStack(gameitem);
                        replymessage = "The Stack Size of all stackable items has been set to their default values (specified in config).";
                        break;
                    }
                    default:
                    {
                        if (int.TryParse(args[0], out stackAmount) == false)
                        {
                            SendReply(player, "Syntax Error: Stack Amount is not a number. Syntax Example: /stackall 65000");
                            return;
                        }
                        replymessage = "The Stack Size of all stackable items has been set to " + stackAmount.ToString() + ".";
                        break;
                    }
                }

                if (gameitem.condition.enabled && gameitem.condition.max > 0 && !(configData.Settings.StackHealthItems)) { continue; }
                if (gameitem.displayName.english.ToString() == "Salt Water" || gameitem.displayName.english.ToString() == "Water") { continue; }

                items.itemlist[gameitem.displayName.english] = Convert.ToInt32(stackAmount);
                gameitem.stackable = Convert.ToInt32(stackAmount);
            }

            SaveData();

            SendReply(player, replymessage);
        }

        [ConsoleCommand("stack")]
        private void StackConsoleCommand(ConsoleSystem.Arg arg)
        {
            if (arg.IsAdmin != true) 
            {
                if ((arg.Connection.userid.ToString() != null) && !(permission.UserHasPermission(arg.Connection.userid.ToString(), "stacksizecontroller.canChangeStackSize")))
                {
                    arg.ReplyWith("[StackSizeController] You don't have permission to use this command.");
                    return;
                }
            }

            if (!pluginLoaded)
            {
                arg.ReplyWith("[StackSizeController] StackSizeController has encountered an error while trying to read the data file. Please contact your server administrator to fix the issue.");
                return;
            }

            if (arg.Args != null)
            {
                if (arg.Args.Length < 2)
                {
                    arg.ReplyWith("[StackSizeController] Syntax Error: Requires 2 arguments. Syntax Example: stack ammo.rocket.hv 64 (Use shortname)");
                    return;
                }
            }
            else
            {
                arg.ReplyWith("[StackSizeController] Syntax Error: Requires 2 arguments. Syntax Example: stack ammo.rocket.hv 64 (Use shortname)");
                return;
            }

            int stackAmount = 0;
            List<ItemDefinition> gameitems = ItemManager.itemList.FindAll(x => x.shortname.Equals(arg.Args[0]));

            if (gameitems.Count == 0)
            {
                arg.ReplyWith("[StackSizeController] Syntax Error: That is an incorrect item name. Please use a valid shortname.");
                return;
            }

            string replymessage = "";
            switch (arg.Args[1].ToLower())
            {
                case "default":
                {
                    stackAmount = DetermineStack(gameitems[0]);
                    replymessage = "[StackSizeController] Updated Stack Size for " + gameitems[0].displayName.english + " (" + gameitems[0].shortname + ") to " + stackAmount + " (Default value based on config).";
                    break;
                }
                default:
                {
                    if (int.TryParse(arg.Args[1], out stackAmount) == false)
                    {
                        arg.ReplyWith("[StackSizeController] Syntax Error: Stack Amount is not a number. Syntax Example: /stack ammo.rocket.hv 64 (Use shortname)");
                        return;
                    }
                    replymessage = "[StackSizeController] Updated Stack Size for " + gameitems[0].displayName.english + " (" + gameitems[0].shortname + ") to " + stackAmount + ".";
                    break;
                }
            }

            if (gameitems[0].condition.enabled && gameitems[0].condition.max > 0)
            {
                if (!(configData.Settings.StackHealthItems))
                {
                    arg.ReplyWith("[StackSizeController] Error: Stacking health items is disabled in the config.");
                    return;
                }
            }

            items.itemlist[gameitems[0].displayName.english] = Convert.ToInt32(stackAmount);

            gameitems[0].stackable = Convert.ToInt32(stackAmount);

            SaveData();

            arg.ReplyWith(replymessage);
        }

        [ConsoleCommand("stackall")]
        private void StackAllConsoleCommand(ConsoleSystem.Arg arg)
        {
            if (arg.IsAdmin != true)
            {
                if ((arg.Connection.userid.ToString() != null) && !(permission.UserHasPermission(arg.Connection.userid.ToString(), "stacksizecontroller.canChangeStackSize")))
                {
                    arg.ReplyWith("[StackSizeController] You don't have permission to use this command.");
                    return;
                }
            }

            if (!pluginLoaded)
            {
                arg.ReplyWith("[StackSizeController] StackSizeController has encountered an error while trying to read the data file. Please contact your server administrator to fix the issue.");
                return;
            }

            if (arg.Args != null)
            {
                if (arg.Args.Length < 1)
                {
                    arg.ReplyWith("[StackSizeController] Syntax Error: Requires 1 argument. Syntax Example: stackall 65000");
                    return;
                }
            }
            else
            {
                arg.ReplyWith("[StackSizeController] Syntax Error: Requires 1 argument. Syntax Example: stackall 65000");
                return;
            }

            int stackAmount = 0;
            string replymessage = "";

            var itemList = ItemManager.itemList;

            foreach (var gameitem in itemList)
            {
                if (gameitem.condition.enabled && gameitem.condition.max > 0 && (!(configData.Settings.StackHealthItems))) { continue; }
                if (gameitem.displayName.english.ToString() == "Salt Water" ||
                gameitem.displayName.english.ToString() == "Water") { continue; }

                switch (arg.Args[0].ToLower())
                {
                    case "default":
                    {
                        stackAmount = DetermineStack(gameitem);
                        replymessage = "[StackSizeController] The Stack Size of all stackable items has been set to their default values (specified in config).";
                        break;
                    }
                    default:
                    {
                        if (int.TryParse(arg.Args[0], out stackAmount) == false)
                        {
                            arg.ReplyWith("[StackSizeController] Syntax Error: Stack Amount is not a number. Syntax Example: /stackall 65000");
                            return;
                        }
                        replymessage = "[StackSizeController] The Stack Size of all stackable items has been set to " + stackAmount.ToString() + ".";
                        break;
                    }
                }

                items.itemlist[gameitem.displayName.english] = Convert.ToInt32(stackAmount);
                gameitem.stackable = Convert.ToInt32(stackAmount);
            }

            SaveData();

            arg.ReplyWith(replymessage);
        }
        #endregion
    }
}