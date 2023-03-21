using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Prism.Events;
using DBCD;
using WDE.Common.Collections;
using WDE.Common.CoreVersion;
using WDE.Common.Database;
using WDE.Common.Database.Counters;
using WDE.Common.DBC;
using WDE.Common.DBC.Structs;
using WDE.Common.Game;
using WDE.Common.Managers;
using WDE.Common.Parameters;
using WDE.Common.Providers;
using WDE.Common.Services;
using WDE.Common.Services.MessageBox;
using WDE.Common.TableData;
using WDE.Common.Tasks;
using WDE.Common.Utils;
using WDE.DbcStore.FastReader;
using WDE.DbcStore.Providers;
using WDE.DbcStore.Spells;
using WDE.DbcStore.Spells.Cataclysm;
using WDE.DbcStore.Spells.Legion;
using WDE.DbcStore.Spells.Wrath;
using WDE.DbcStore.Structs;
using WDE.Module.Attributes;
using WDE.MVVM.Observable;

namespace WDE.DbcStore
{
    public enum DBCVersions
    {
        WOTLK_12340 = 12340,
        CATA_15595 = 15595,
        MOP_18414 = 18414,
        LEGION_26972 = 26972,
        SHADOWLANDS_41079 = 41079
    }

    public enum DBCLocales
    {
        LANG_enUS = 0,
        LANG_enGB = LANG_enUS,
        LANG_koKR = 1,
        LANG_frFR = 2,
        LANG_deDE = 3,
        LANG_enCN = 4,
        LANG_zhCN = LANG_enCN,
        LANG_enTW = 5,
        LANG_zhTW = LANG_enTW,
        LANG_esES = 6,
        LANG_esMX = 7,
        LANG_ruRU = 8,
        LANG_ptPT = 10,
        LANG_ptBR = LANG_ptPT,
        LANG_itIT = 11
    }

    [AutoRegister]
    [SingleInstance]
    public class DbcStore : IDbcStore, IDbcSpellService, IMapAreaStore, IFactionTemplateStore
    {
        private readonly IDbcSettingsProvider dbcSettingsProvider;
        private readonly IMessageBoxService messageBoxService;
        private readonly IEventAggregator eventAggregator;
        private readonly ICurrentCoreVersion currentCoreVersion;
        private readonly ITabularDataPicker dataPicker;
        private readonly IWindowManager windowManager;
        private readonly IDatabaseRowsCountProvider databaseRowsCountProvider;
        private readonly NullSpellService nullSpellService;
        private readonly CataSpellService cataSpellService;
        private readonly WrathSpellService wrathSpellService;
        private readonly LegionSpellService legionSpellService;
        private readonly DatabaseClientFileOpener opener;
        private readonly IParameterFactory parameterFactory;
        private readonly ITaskRunner taskRunner;
        private readonly DBCD.DBCD dbcd;

        public DbcStore(IParameterFactory parameterFactory, 
            ITaskRunner taskRunner,
            IDbcSettingsProvider settingsProvider,
            IMessageBoxService messageBoxService,
            IEventAggregator eventAggregator,
            ICurrentCoreVersion currentCoreVersion,
            ITabularDataPicker dataPicker,
            IWindowManager windowManager,
            IDatabaseRowsCountProvider databaseRowsCountProvider,
            NullSpellService nullSpellService,
            CataSpellService cataSpellService,
            WrathSpellService wrathSpellService,
            LegionSpellService legionSpellService,
            DatabaseClientFileOpener opener,
            DBCD.DBCD dbcd)
        {
            this.parameterFactory = parameterFactory;
            this.taskRunner = taskRunner;
            dbcSettingsProvider = settingsProvider;
            this.messageBoxService = messageBoxService;
            this.eventAggregator = eventAggregator;
            this.currentCoreVersion = currentCoreVersion;
            this.dataPicker = dataPicker;
            this.windowManager = windowManager;
            this.databaseRowsCountProvider = databaseRowsCountProvider;
            this.nullSpellService = nullSpellService;
            this.cataSpellService = cataSpellService;
            this.wrathSpellService = wrathSpellService;
            this.legionSpellService = legionSpellService;
            this.opener = opener;
            this.dbcd = dbcd;

            spellServiceImpl = nullSpellService;
            Load();
        }
        
        public bool IsConfigured { get; private set; }
        public Dictionary<long, string> AreaTriggerStore { get; internal set; } = new();
        public Dictionary<long, long> FactionTemplateStore { get; internal set; } = new();
        public Dictionary<long, string> FactionStore { get; internal set; } = new();
        public Dictionary<long, string> SpellStore { get; internal set; } = new();
        public Dictionary<long, string> SkillStore { get; internal set;} = new();
        public Dictionary<long, string> LanguageStore { get; internal set;} = new();
        public Dictionary<long, string> PhaseStore { get; internal set;} = new();
        public Dictionary<long, string> AreaStore { get; internal set;} = new();
        public Dictionary<long, string> MapStore { get; internal set;} = new();
        public Dictionary<long, string> SoundStore { get;internal set; } = new();
        public Dictionary<long, string> MovieStore { get; internal set;} = new();
        public Dictionary<long, string> CurrencyTypeStore { get; internal set;} = new();
        public Dictionary<long, string> ClassStore { get; internal set;} = new();
        public Dictionary<long, string> RaceStore { get; internal set;} = new();
        public Dictionary<long, string> EmoteStore { get;internal set; } = new();
        public Dictionary<long, string> EmoteOneShotStore { get;internal set; } = new();
        public Dictionary<long, string> EmoteStateStore { get;internal set; } = new();
        public Dictionary<long, string> TextEmoteStore { get;internal set; } = new();
        public Dictionary<long, string> AchievementStore { get; internal set;} = new();
        public Dictionary<long, string> ItemStore { get; internal set;} = new();
        public Dictionary<long, string> SpellFocusObjectStore { get; internal set; } = new();
        public Dictionary<long, string> QuestInfoStore { get; internal set; } = new();
        public Dictionary<long, string> CharTitleStore { get; internal set; } = new();
        public Dictionary<long, string> CreatureModelDataStore {get; internal set; } = new();
        public Dictionary<long, string> GameObjectDisplayInfoStore {get; internal set; } = new();
        public Dictionary<long, string> MapDirectoryStore { get; internal set;} = new();
        public Dictionary<long, string> QuestSortStore { get; internal set;} = new();
        public Dictionary<long, string> SceneStore { get; internal set; } = new();
        public Dictionary<long, string> ScenarioStore { get; internal set;} = new();
        public Dictionary<long, string> ScenarioStepStore { get; internal set;} = new();
        public Dictionary<long, Dictionary<long, long>> ScenarioToStepStore { get; internal set; } = new();
        public Dictionary<long, long> BattlePetSpeciesIdStore { get; internal set; } = new();

        public IReadOnlyList<IArea> Areas { get; internal set; } = Array.Empty<IArea>();
        public Dictionary<uint, IArea> AreaById { get; internal set; } = new();

        public IReadOnlyList<IMap> Maps { get; internal set; } = Array.Empty<IMap>();
        public Dictionary<uint, IMap> MapById { get; internal set; } = new();
        
        public IReadOnlyList<FactionTemplate> FactionTemplates { get; internal set; } = Array.Empty<FactionTemplate>();
        public Dictionary<uint, FactionTemplate> FactionTemplateById { get; internal set; } = new();
        
        public IReadOnlyList<Faction> Factions { get; internal set; } = Array.Empty<Faction>();
        public Dictionary<ushort, Faction> FactionsById { get; internal set; } = new();
        
        public IArea? GetAreaById(uint id) => AreaById.TryGetValue(id, out var area) ? area : null;
        public IMap? GetMapById(uint id) => MapById.TryGetValue(id, out var map) ? map : null;
        public FactionTemplate? GetFactionTemplate(uint templateId) => FactionTemplateById.TryGetValue(templateId, out var faction) ? faction : null;
        public Faction? GetFaction(ushort factionId) => FactionsById.TryGetValue(factionId, out var faction) ? faction : null;
        
        internal void Load()
        {            
            parameterFactory.Register("RaceMaskParameter", new RaceMaskParameter(currentCoreVersion.Current.GameVersionFeatures.AllRaces), QuickAccessMode.Limited);

            if (dbcSettingsProvider.GetSettings().SkipLoading ||
                !Directory.Exists(dbcSettingsProvider.GetSettings().Path))
            {
                // we create a new fake task, that will not be started, but finalized so that (empty) parameters are registered
                var fakeTask = new DbcLoadTask(parameterFactory, dataPicker, opener, dbcSettingsProvider, this);
                fakeTask.FinishMainThread();
                return;
            }

            IsConfigured = true;
            taskRunner.ScheduleTask(new DbcLoadTask(parameterFactory, dataPicker, opener, dbcSettingsProvider, this));
        }

        private class DbcLoadTask : IThreadedTask
        {
            private readonly IDbcSettingsProvider dbcSettingsProvider;
            private readonly IParameterFactory parameterFactory;
            private readonly ITabularDataPicker dataPicker;
            private readonly DbcStore store;
            private readonly DBDProvider dbdProvider = null!;
            private readonly DBCProvider dbcProvider = null!;

            private Dictionary<long, string> AreaTriggerStore { get; } = new();
            private Dictionary<long, long> FactionTemplateStore { get; } = new();
            private Dictionary<long, string> FactionStore { get; } = new();
            private Dictionary<long, string> SpellStore { get; } = new();
            public Dictionary<long, string> SkillStore { get; } = new();
            public Dictionary<long, string> LanguageStore { get; } = new();
            public Dictionary<long, string> PhaseStore { get; } = new();
            public Dictionary<long, string> AreaStore { get; } = new();
            public Dictionary<long, string> MapStore { get; } = new();
            public Dictionary<long, string> SoundStore { get; } = new();
            public Dictionary<long, string> MovieStore { get; } = new();
            public Dictionary<long, string> ClassStore { get; } = new();
            public Dictionary<long, string> RaceStore { get; } = new();
            public Dictionary<long, string> EmoteStore { get; } = new();
            public Dictionary<long, string> EmoteOneShotStore { get; } = new();
            public Dictionary<long, string> EmoteStateStore { get; } = new();
            public Dictionary<long, string> TextEmoteStore { get; } = new();
            public Dictionary<long, string> AchievementStore { get; } = new();
            public Dictionary<long, string> ItemStore { get; } = new();
            public Dictionary<long, string> SpellFocusObjectStore { get; } = new();
            public Dictionary<long, string> QuestInfoStore { get; } = new();
            public Dictionary<long, string> CharTitleStore { get; } = new();
            private Dictionary<long, long> CreatureDisplayInfoStore { get; } = new();
            public Dictionary<long, string> CreatureModelDataStore { get; } = new();
            public Dictionary<long, string> GameObjectDisplayInfoStore { get; } = new();
            public Dictionary<long, string> MapDirectoryStore { get; internal set;} = new();
            public Dictionary<long, string> QuestSortStore { get; internal set;} = new();
            public Dictionary<long, string> CurrencyTypeStore { get; internal set;} = new();
            public Dictionary<long, string> ExtendedCostStore { get; internal set;} = new();
            public Dictionary<long, string> TaxiNodeStore { get; internal set;} = new();
            public Dictionary<long, (int, int)> TaxiPathsStore { get; internal set;} = new();
            public Dictionary<long, string> SpellItemEnchantmentStore { get; internal set;} = new();
            public Dictionary<long, string> AreaGroupStore { get; internal set;} = new();
            public Dictionary<long, string> ItemDisplayInfoStore { get; internal set;} = new();
            public Dictionary<long, string> MailTemplateStore { get; internal set;} = new();
            public Dictionary<long, string> LFGDungeonStore { get; internal set;} = new();
            public Dictionary<long, string> ItemSetStore { get; internal set;} = new();
            public Dictionary<long, string> DungeonEncounterStore { get; internal set;} = new();
            public Dictionary<long, string> HolidayNamesStore { get; internal set;} = new();
            public Dictionary<long, string> HolidaysStore { get; internal set;} = new();
            public Dictionary<long, string> WorldSafeLocsStore { get; internal set;} = new();
            public Dictionary<long, string> BattlegroundStore { get; internal set;} = new();
            public Dictionary<long, string> AchievementCriteriaStore { get; internal set;} = new();
            public Dictionary<long, string> ItemDbcStore { get; internal set;} = new(); // item.dbc, not item-sparse.dbc
            public Dictionary<long, string> SceneStore { get; internal set;} = new();
            public Dictionary<long, string> ScenarioStore { get; internal set;} = new();
            public Dictionary<long, string> ScenarioStepStore { get; internal set;} = new();
            public Dictionary<long, string> BattlePetAbilityStore { get; internal set;} = new();
            public Dictionary<long, Dictionary<long, long>> ScenarioToStepStore { get; internal set; } = new();
            public Dictionary<long, long> BattlePetSpeciesIdStore { get; internal set; } = new();
            public Dictionary<long, string> CharSpecializationStore { get; internal set;} = new();
            public Dictionary<long, string> GarrisonClassSpecStore { get; internal set; } = new();
            public Dictionary<long, string> GarrisonBuildingStore { get; internal set; } = new();
            public Dictionary<long, string> GarrisonTalentStore { get; internal set; } = new();
            public Dictionary<long, string> DifficultyStore { get; internal set; } = new();
            public Dictionary<long, string> LockTypeStore { get; internal set; } = new();
            public Dictionary<long, string> VignetteStore { get; internal set; } = new();
            public Dictionary<long, string> AdventureJournalStore { get; internal set; } = new();
            
            private List<(string parameter, Dictionary<long, SelectOption> options)> parametersToRegister = new();
            public List<AreaEntry> Areas { get; } = new();
            public List<MapEntry> Maps { get; } = new();
            public List<FactionTemplate> FactionTemplates { get; } = new();
            public List<Faction> Factions { get; } = new();
             
            public string Name => "DBC Loading";
            public bool WaitForOtherTasks => false;
            private DatabaseClientFileOpener opener;
            
            public DbcLoadTask(IParameterFactory parameterFactory,
                ITabularDataPicker dataPicker,
                DatabaseClientFileOpener opener,
                IDbcSettingsProvider settingsProvider, 
                DbcStore store)
            {
                this.parameterFactory = parameterFactory;
                this.dataPicker = dataPicker;
                this.store = store;
                this.opener = opener;
                dbcSettingsProvider = settingsProvider;
            }

            private void LoadAndRegister(string filename, string parameter, int keyIndex, Func<IDbcIterator, string> getString)
            {
                Dictionary<long, SelectOption> dict = new();
                Load(filename, row =>
                {
                    var id = row.GetUInt(keyIndex);
                    dict[id] = new SelectOption(getString(row));
                });
                parametersToRegister.Add((parameter, dict));
            }

            private void LoadAndRegister(string filename, string parameter, int keyIndex, int nameIndex, bool withLocale = false)
            {
                int locale = (int) DBCLocales.LANG_enUS;

                if (withLocale)
                    locale = (int) dbcSettingsProvider.GetSettings().DBCLocale;
                LoadAndRegister(filename, parameter, keyIndex, r => r.GetString(nameIndex + locale));
            }

            private void Load(string filename, Action<IDbcIterator> foreachRow)
            {
                progress?.Report(now++, max, $"Loading {filename}");
                var path = $"{dbcSettingsProvider.GetSettings().Path}/{filename}";

                if (!File.Exists(path))
                    return;

                foreach (var entry in opener.Open(path))
                    foreachRow(entry);
            }
            
            private void Load(string filename, int id, int nameIndex, Dictionary<long, string> dictionary, bool useLocale = false)
            {
                int locale = (int) DBCLocales.LANG_enUS;

                if (useLocale)
                    locale = LocaleOffset;

                Load(filename, row => dictionary.Add(row.GetInt(id), row.GetString(nameIndex + locale)));
            }
            
            private int LocaleOffset => (int) dbcSettingsProvider.GetSettings().DBCLocale;
            
            private void Load(string filename, int id, int nameIndex, Dictionary<long, long> dictionary)
            {
                Load(filename, row => dictionary.Add(row.GetInt(id), row.GetInt(nameIndex)));
            }

            private void LoadDB2(string filename, Action<DBCDRow> doAction)
            {
                var storage = store.dbcd.Load($"{dbcSettingsProvider.GetSettings().Path}/{filename}");
                foreach (DBCDRow item in storage.Values)
                    doAction(item);
            }
            
            private void Load(string filename, string fieldName, Dictionary<long, string> dictionary)
            {
                var storage = store.dbcd.Load($"{dbcSettingsProvider.GetSettings().Path}/{filename}");

                if (fieldName == String.Empty)
                {
                    foreach (DBCDRow item in storage.Values)
                        dictionary.Add(item.ID, String.Empty);
                }
                else
                {
                    foreach (DBCDRow item in storage.Values)
                    {
                        if (item[fieldName] == null)
                            return;

                        dictionary.Add(item.ID, item[fieldName].ToString()!);
                    }
                }
            }

            private void Load(string filename, string fieldName, Dictionary<long, long> dictionary)
            {
                var storage = store.dbcd.Load($"{dbcSettingsProvider.GetSettings().Path}/{filename}");

                if (fieldName == String.Empty)
                {
                    foreach (DBCDRow item in storage.Values)
                        dictionary.Add(item.ID, 0);
                }
                else
                {
                    foreach (DBCDRow item in storage.Values)
                    {
                        if (item[fieldName] == null)
                            return;

                        dictionary.Add(item.ID, Convert.ToInt64(item[fieldName]));
                    }
                }
            }

            public void FinishMainThread()
            {
                store.AreaTriggerStore = AreaTriggerStore;
                store.FactionStore = FactionStore;
                store.SpellStore = SpellStore;
                store.SkillStore = SkillStore;
                store.LanguageStore = LanguageStore;
                store.PhaseStore = PhaseStore;
                store.AreaStore = AreaStore;
                store.MapStore = MapStore;
                store.SoundStore = SoundStore;
                store.MovieStore = MovieStore;
                store.ClassStore = ClassStore;
                store.RaceStore = RaceStore;
                store.EmoteStore = EmoteStore;
                store.EmoteOneShotStore = EmoteOneShotStore;
                store.EmoteStateStore = EmoteStateStore;
                store.TextEmoteStore = TextEmoteStore;
                store.AchievementStore = AchievementStore;
                store.ItemStore = ItemStore;
                store.SpellFocusObjectStore = SpellFocusObjectStore;
                store.QuestInfoStore = QuestInfoStore;
                store.CharTitleStore = CharTitleStore;
                store.CreatureModelDataStore = CreatureModelDataStore;
                store.GameObjectDisplayInfoStore = GameObjectDisplayInfoStore;
                store.MapDirectoryStore = MapDirectoryStore;
                store.QuestSortStore = QuestSortStore;
                store.SceneStore = SceneStore;
                store.ScenarioStore = ScenarioStore;
                store.ScenarioStepStore = ScenarioStepStore;
                store.ScenarioToStepStore = ScenarioToStepStore;
                store.BattlePetSpeciesIdStore = BattlePetSpeciesIdStore;
                store.CurrencyTypeStore = CurrencyTypeStore;
                store.Areas = Areas;
                store.AreaById = Areas.ToDictionary(a => a.Id, a => (IArea)a);
                store.Maps = Maps;
                store.MapById = Maps.ToDictionary(a => a.Id, a => (IMap)a);
                store.FactionTemplates = FactionTemplates;
                store.FactionTemplateById = FactionTemplates.ToDictionary(a => a.TemplateId, a => (FactionTemplate)a);
                store.Factions = Factions;
                store.FactionsById = Factions.ToDictionary(a => a.FactionId, a => a);

                foreach (var (parameterName, options) in parametersToRegister)
                {
                    parameterFactory.Register(parameterName, new Parameter()
                    {
                        Items = options
                    });
                }
                parametersToRegister.Clear();
                
                parameterFactory.Register("AchievementParameter", new DbcParameter(AchievementStore), QuickAccessMode.Full);
                parameterFactory.Register("MovieParameter", new DbcParameter(MovieStore), QuickAccessMode.Limited);
                parameterFactory.Register("RawFactionParameter", new DbcParameter(FactionStore), QuickAccessMode.Limited);
                parameterFactory.Register("FactionParameter", new FactionParameter(FactionStore, FactionTemplateStore), QuickAccessMode.Limited);
                parameterFactory.Register("DbcSpellParameter", new DbcParameter(SpellStore));
                parameterFactory.Register("CurrencyTypeParameter", new DbcParameter(CurrencyTypeStore));
                parameterFactory.Register("ItemDbcParameter", new DbcParameter(ItemStore));
                parameterFactory.Register("EmoteParameter", new DbcParameter(EmoteStore), QuickAccessMode.Full);
                parameterFactory.Register("EmoteOneShotParameter", new DbcParameter(EmoteOneShotStore));
                parameterFactory.Register("EmoteStateParameter", new DbcParameter(EmoteStateStore));
                parameterFactory.Register("TextEmoteParameter", new DbcParameter(TextEmoteStore), QuickAccessMode.Limited);
                parameterFactory.Register("ClassParameter", new DbcParameter(ClassStore), QuickAccessMode.Limited);
                parameterFactory.Register("ClassMaskParameter", new DbcMaskParameter(ClassStore, -1));
                parameterFactory.Register("RaceParameter", new DbcParameter(RaceStore));
                parameterFactory.Register("SkillParameter", new DbcParameter(SkillStore), QuickAccessMode.Limited);
                parameterFactory.Register("SoundParameter", new DbcParameter(SoundStore), QuickAccessMode.Limited);
                parameterFactory.Register("MapParameter", new DbcParameter(MapStore), QuickAccessMode.Limited);
                parameterFactory.Register("DbcPhaseParameter", new DbcParameter(PhaseStore), QuickAccessMode.Limited);
                parameterFactory.Register("SpellFocusObjectParameter", new DbcParameter(SpellFocusObjectStore), QuickAccessMode.Limited);
                parameterFactory.Register("QuestInfoParameter", new DbcParameter(QuestInfoStore));
                parameterFactory.Register("CharTitleParameter", new DbcParameter(CharTitleStore));
                parameterFactory.Register("ExtendedCostParameter", new DbcParameter(ExtendedCostStore));
                parameterFactory.Register("CreatureModelDataParameter", new CreatureModelParameter(CreatureModelDataStore, CreatureDisplayInfoStore));
                parameterFactory.Register("GameObjectDisplayInfoParameter", new DbcFileParameter(GameObjectDisplayInfoStore));
                parameterFactory.Register("LanguageParameter", new LanguageParameter(LanguageStore), QuickAccessMode.Limited);
                parameterFactory.Register("AreaTriggerParameter", new DbcParameter(AreaTriggerStore));
                parameterFactory.Register("ZoneOrQuestSortParameter", new ZoneOrQuestSortParameter(AreaStore, QuestSortStore));
                parameterFactory.Register("TaxiPathParameter", new TaxiPathParameter(TaxiPathsStore, TaxiNodeStore));
                parameterFactory.Register("TaxiNodeParameter", new DbcParameter(TaxiNodeStore));
                parameterFactory.Register("SpellItemEnchantmentParameter", new DbcParameter(SpellItemEnchantmentStore));
                parameterFactory.Register("AreaGroupParameter", new DbcParameter(AreaGroupStore));
                parameterFactory.Register("ItemDisplayInfoParameter", new DbcParameter(ItemDisplayInfoStore));
                parameterFactory.Register("MailTemplateParameter", new DbcParameter(MailTemplateStore));
                parameterFactory.Register("LFGDungeonParameter", new DbcParameter(LFGDungeonStore));
                parameterFactory.Register("ItemSetParameter", new DbcParameter(ItemSetStore));
                parameterFactory.Register("DungeonEncounterParameter", new DbcParameter(DungeonEncounterStore));
                parameterFactory.Register("HolidaysParameter", new DbcParameter(HolidaysStore));
                parameterFactory.Register("WorldSafeLocParameter", new DbcParameter(WorldSafeLocsStore));
                parameterFactory.Register("BattlegroundParameter", new DbcParameter(BattlegroundStore));
                parameterFactory.Register("AchievementCriteriaParameter", new DbcParameter(AchievementCriteriaStore));
                parameterFactory.Register("ItemVisualParameter", new DbcParameter(ItemDbcStore));
                parameterFactory.Register("SceneScriptParameter", new DbcParameter(SceneStore));
                parameterFactory.Register("ScenarioParameter", new DbcParameter(ScenarioStore));
                parameterFactory.Register("ScenarioStepParameter", new DbcParameter(ScenarioStepStore));
                parameterFactory.Register("BattlePetAbilityParameter", new DbcParameter(BattlePetAbilityStore));
                parameterFactory.Register("CharSpecializationParameter", new DbcParameter(CharSpecializationStore));
                parameterFactory.Register("GarrisonClassSpecParameter", new DbcParameter(GarrisonClassSpecStore));
                parameterFactory.Register("GarrisonBuildingParameter", new DbcParameter(GarrisonBuildingStore));
                parameterFactory.Register("GarrisonTalentParameter", new DbcParameter(GarrisonTalentStore));
                parameterFactory.Register("DifficultyParameter", new DbcParameter(DifficultyStore));
                parameterFactory.Register("LockTypeParameter", new DbcParameter(LockTypeStore));
                parameterFactory.Register("AdventureJournalParameter", new DbcParameter(AdventureJournalStore));
                parameterFactory.Register("VignetteParameter", new DbcParameterWowTools(VignetteStore, "vignette", store.currentCoreVersion, store.windowManager));
                parameterFactory.Register("VehicleParameter", new WoWToolsParameter("vehicle", store.currentCoreVersion, store.windowManager));
                parameterFactory.Register("LockParameter", new WoWToolsParameter("lock", store.currentCoreVersion, store.windowManager));

                void RegisterZoneAreParameter(string key, TabularDataAsyncColumn<uint>? counterColumn = null)
                {
                    parameterFactory.Register(key, 
                        new DbcParameterWithPicker<IArea>(dataPicker, AreaStore, "zone or area", area => area.Id,
                            () => store.Areas,
                            (area, text) => area.Name.Contains(text, StringComparison.InvariantCultureIgnoreCase) || area.Id.Contains(text),
                            new TabularDataColumn(nameof(IArea.Id), "Entry", 60), 
                            new TabularDataColumn(nameof(IArea.Name), "Name", 160), 
                            new TabularDataColumn(nameof(IArea.ParentArea) + "." + nameof(IArea.Name), "Parent", 160), 
                            new TabularDataColumn(nameof(IArea.Map) + "." + nameof(IMap.Name), "Map", 120),
                            counterColumn), QuickAccessMode.Limited);   
                }
                RegisterZoneAreParameter("ZoneAreaParameter");
                RegisterZoneAreParameter("ZoneArea(spell_area)Parameter", 
                    new TabularDataAsyncColumn<uint>(nameof(IArea.Id), "Count", async (zoneId, token) =>
                {
                    if (zoneId == 0)
                        return "0";
                    return (await store.databaseRowsCountProvider.GetRowsCountByPrimaryKey("spell_area", zoneId, token)).ToString();
                }, 50));
                RegisterZoneAreParameter("ZoneArea(phase_definitions)Parameter", 
                    new TabularDataAsyncColumn<uint>(nameof(IArea.Id), "Count", async (zoneId, token) =>
                {
                    if (zoneId == 0)
                        return "0";
                    return (await store.databaseRowsCountProvider.GetRowsCountByPrimaryKey("phase_definitions", zoneId, token)).ToString();
                }, 50));

                parameterFactory.RegisterDepending("BattlePetSpeciesParameter", "CreatureParameter", (creature) => new BattlePetSpeciesParameter(store, parameterFactory, creature));

                switch (dbcSettingsProvider.GetSettings().DBCVersion)
                {
                    case DBCVersions.WOTLK_12340:
                        store.spellServiceImpl = store.wrathSpellService;
                        break;
                    case DBCVersions.CATA_15595:
                        store.spellServiceImpl = store.cataSpellService;
                        break;
                    case DBCVersions.LEGION_26972:
                        store.spellServiceImpl = store.legionSpellService;
                        break;
                }

                store.spellServiceImpl.Changed += _ => store.InvokeChangedSpells();
                store.InvokeChangedSpells();
                
                store.eventAggregator.GetEvent<DbcLoadedEvent>().Publish(store);
            }

            private int max = 0;
            private int now = 0;
            private ITaskProgress progress = null!;
            
            public void Run(ITaskProgress progress)
            {
                this.progress = progress;

                switch (dbcSettingsProvider.GetSettings().DBCVersion)
                {
                    case DBCVersions.WOTLK_12340:
                    {
                        store.wrathSpellService.Load(dbcSettingsProvider.GetSettings().Path);
                        max = 43;
                        Load("AreaTrigger.dbc", row => AreaTriggerStore.Add(row.GetInt(0), $"Area trigger"));
                        Load("SkillLine.dbc", 0, 3, SkillStore, true);
                        Load("Faction.dbc", row =>
                        {
                            var faction = new Faction(row.GetUShort(0), row.GetString(23 + LocaleOffset));
                            Factions.Add(faction);
                            FactionStore[faction.FactionId] = faction.Name;
                        });
                        Load("FactionTemplate.dbc", row =>
                        {
                            var template = new FactionTemplate()
                            {
                                TemplateId = row.GetUInt(0),
                                Faction = row.GetUShort(1),
                                Flags = row.GetUShort(2),
                                FactionGroup = (FactionGroupMask)row.GetUShort(3),
                                FriendGroup = (FactionGroupMask)row.GetUShort(4),
                                EnemyGroup = (FactionGroupMask)row.GetUShort(5)
                            };
                            FactionTemplates.Add(template);
                            FactionTemplateStore[row.GetUInt(0)] = row.GetUInt(1);
                        });
                        Load("Spell.dbc", 0, 136, SpellStore, true);
                        Load("Movie.dbc", 0, 1, MovieStore);
                        Load("Map.dbc", row =>
                        {
                            var map = new MapEntry()
                            {
                                Id = row.GetUInt(0),
                                Name = row.GetString(5 + LocaleOffset),
                                Directory = row.GetString(1),
                                Type = (InstanceType)row.GetUInt(2),
                            };
                            Maps.Add(map);
                        });
                        Load("Achievement.dbc", 0, 4, AchievementStore, true);
                        Load("AreaTable.dbc", row =>
                        {
                            var entry = new AreaEntry()
                            {
                                Id = row.GetUInt(0),
                                MapId = row.GetUInt(1),
                                ParentAreaId = row.GetUInt(2),
                                Flags1 = row.GetUInt(4),
                                Name = row.GetString(11 + LocaleOffset)
                            };
                            Areas.Add(entry);
                        });
                        FillMapAreas();
                        Load("chrClasses.dbc", 0, 4, ClassStore, true);
                        Load("chrRaces.dbc", 0, 14, RaceStore, true);
                        Load("Emotes.dbc", row =>
                        {
                            var proc = row.GetUInt(4);
                            if (proc == 0)
                                EmoteOneShotStore.Add(row.GetUInt(0), row.GetString(1));
                            else if (proc == 2)
                                EmoteStateStore.Add(row.GetUInt(0), row.GetString(1));
                            EmoteStore.Add(row.GetUInt(0), row.GetString(1));
                        });
                        Load("EmotesText.dbc", 0, 1, TextEmoteStore);
                        Load("SoundEntries.dbc", 0, 2, SoundStore);
                        Load("SpellFocusObject.dbc", 0, 1, SpellFocusObjectStore, true);
                        Load("QuestInfo.dbc", 0, 1, QuestInfoStore, true);
                        Load("CharTitles.dbc", 0, 2, CharTitleStore, true);
                        Load("CreatureModelData.dbc", 0, 2, CreatureModelDataStore);
                        Load("CreatureDisplayInfo.dbc", 0, 1, CreatureDisplayInfoStore);
                        Load("GameObjectDisplayInfo.dbc", 0, 1, GameObjectDisplayInfoStore);
                        Load("Languages.dbc", 0, 1, LanguageStore, true);
                        Load("QuestSort.dbc", 0, 1, QuestSortStore, true);
                        Load("ItemExtendedCost.dbc", row => ExtendedCostStore.Add(row.GetInt(0), GenerateCostDescription(row.GetInt(1), row.GetInt(2), row.GetInt(4))));
                        Load("TaxiNodes.dbc", 0, 5, TaxiNodeStore, true);
                        Load("TaxiPath.dbc",  row => TaxiPathsStore.Add(row.GetUInt(0), (row.GetInt(1), row.GetInt(2))));
                        Load("SpellItemEnchantment.dbc", 0, 14, SpellItemEnchantmentStore, true);
                        Load("AreaGroup.dbc",  row => AreaGroupStore.Add(row.GetUInt(0), BuildAreaGroupName(row, 1, 6)));
                        Load("ItemDisplayInfo.dbc", 0, 5, ItemDisplayInfoStore);
                        Load("MailTemplate.dbc", row =>
                        {
                            int locale = (int) dbcSettingsProvider.GetSettings().DBCLocale;
                            var subject = row.GetString(1 + locale);
                            var body = row.GetString(18 + locale);
                            var name = string.IsNullOrEmpty(subject) ? body.TrimToLength(50) : subject;
                            MailTemplateStore.Add(row.GetUInt(0), name.Replace("\n", ""));
                        });
                        Load("LFGDungeons.dbc", 0, 1, LFGDungeonStore, true);
                        Load("ItemSet.dbc", 0, 1, ItemSetStore, true);
                        Load("DungeonEncounter.dbc", 0, 5, DungeonEncounterStore, true);
                        Load("HolidayNames.dbc", 0, 1, HolidayNamesStore, true);
                        Load("Holidays.dbc", row =>
                        {
                            var id = row.GetUInt(0);
                            var nameId = row.GetUInt(49);
                            if (HolidayNamesStore.TryGetValue(nameId, out var name))
                                HolidaysStore[id] = name;
                            else
                                HolidaysStore[id] = "Holiday " + id;
                        });
                        Load("WorldSafeLocs.dbc", 0, 5, WorldSafeLocsStore, true);
                        Load("BattlemasterList.dbc", 0, 11, BattlegroundStore, true);
                        Load("Achievement_Criteria.dbc", 0, 9, AchievementCriteriaStore, true);
                        Load("Item.dbc", row =>
                        {
                            var id = row.GetUInt(0);
                            var displayId = row.GetUInt(5);
                            if (ItemDisplayInfoStore.TryGetValue(displayId, out var name))
                                ItemDbcStore[id] = name;
                            else
                                ItemDbcStore[id] = "Item " + id;
                        });
                        Load("LockType.dbc", 0, 1, LockTypeStore, true);
                        LoadAndRegister("SpellCastTimes.dbc", "SpellCastTimeParameter", 0, row => GetCastTimeDescription(row.GetInt(1), row.GetInt(2), row.GetInt(3)));
                        LoadAndRegister("SpellDuration.dbc", "SpellDurationParameter", 0, row => GetDurationTimeDescription(row.GetInt(1), row.GetInt(2), row.GetInt(3)));
                        LoadAndRegister("SpellRange.dbc", "SpellRangeParameter", 0, 6, true);
                        LoadAndRegister("SpellRadius.dbc", "SpellRadiusParameter", 0, row => GetRadiusDescription(row.GetFloat(1), row.GetFloat(2), row.GetFloat(3)));
                        break;
                    }
                    case DBCVersions.CATA_15595:
                    {
                        store.cataSpellService.Load(dbcSettingsProvider.GetSettings().Path);
                        max = 44;
                        Load("AreaTrigger.dbc", row => AreaTriggerStore.Add(row.GetInt(0), $"Area trigger"));
                        Load("SkillLine.dbc", 0, 2, SkillStore);
                        Load("Faction.dbc", row =>
                        {
                            var faction = new Faction(row.GetUShort(0), row.GetString(23));
                            Factions.Add(faction);
                            FactionStore[faction.FactionId] = faction.Name;
                        });
                        Load("FactionTemplate.dbc", row =>
                        {
                            var template = new FactionTemplate()
                            {
                                TemplateId = row.GetUInt(0),
                                Faction = row.GetUShort(1),
                                Flags = row.GetUShort(2),
                                FactionGroup = (FactionGroupMask)row.GetUShort(3),
                                FriendGroup = (FactionGroupMask)row.GetUShort(4),
                                EnemyGroup = (FactionGroupMask)row.GetUShort(5)
                            };
                            FactionTemplates.Add(template);
                            FactionTemplateStore[row.GetUInt(0)] = row.GetUInt(1);
                        });
                        Load("CurrencyTypes.db2", 0, 2, CurrencyTypeStore);
                        Load("Spell.dbc", 0, 21, SpellStore);
                        Load("Movie.dbc", 0, 1, MovieStore);
                        Load("Map.dbc", row =>
                        {
                            var map = new MapEntry()
                            {
                                Id = row.GetUInt(0),
                                Name = row.GetString(6),
                                Directory = row.GetString(1),
                                Type = (InstanceType)row.GetUInt(2),
                            };
                            Maps.Add(map);
                        });
                        Load("Achievement.dbc", 0, 4, AchievementStore);
                        Load("AreaTable.dbc", row =>
                        {
                            var entry = new AreaEntry()
                            {
                                Id = row.GetUInt(0),
                                MapId = row.GetUInt(1),
                                ParentAreaId = row.GetUInt(2),
                                Flags1 = row.GetUInt(4),
                                Name = row.GetString(11)
                            };
                            Areas.Add(entry);
                        });
                        FillMapAreas();
                        Load("chrClasses.dbc", 0, 3, ClassStore);
                        Load("chrRaces.dbc", 0, 14, RaceStore);
                        Load("Emotes.dbc", row =>
                        {
                            var proc = row.GetUInt(4);
                            if (proc == 0)
                                EmoteOneShotStore.Add(row.GetUInt(0), row.GetString(1));
                            else if (proc == 2)
                                EmoteStateStore.Add(row.GetUInt(0), row.GetString(1));
                            EmoteStore.Add(row.GetUInt(0), row.GetString(1));
                        });
                        Load("EmotesText.dbc", 0, 1, TextEmoteStore);
                        Load("item-sparse.db2", 0, 99, ItemStore);
                        Load("Phase.dbc", 0, 1, PhaseStore);
                        Load("SoundEntries.dbc", 0, 2, SoundStore);
                        Load("SpellFocusObject.dbc", 0, 1, SpellFocusObjectStore);
                        Load("QuestInfo.dbc", 0, 1, QuestInfoStore);
                        Load("CharTitles.dbc", 0, 2, CharTitleStore);
                        Load("CreatureModelData.dbc", 0, 2, CreatureModelDataStore);
                        Load("CreatureDisplayInfo.dbc", 0, 1, CreatureDisplayInfoStore);
                        Load("GameObjectDisplayInfo.dbc", 0, 1, GameObjectDisplayInfoStore);
                        Load("Languages.dbc", 0, 1, LanguageStore);
                        Load("QuestSort.dbc", 0, 1, QuestSortStore);
                        Load("ItemExtendedCost.dbc", row => ExtendedCostStore.Add(row.GetInt(0), GenerateCostDescription(row.GetInt(1), row.GetInt(2), row.GetInt(4))));
                        Load("TaxiNodes.dbc", 0, 5, TaxiNodeStore);
                        Load("TaxiPath.dbc",  row => TaxiPathsStore.Add(row.GetUInt(0), (row.GetInt(1), row.GetInt(2))));
                        Load("SpellItemEnchantment.dbc", 0, 14, SpellItemEnchantmentStore);
                        Load("AreaGroup.dbc",  row => AreaGroupStore.Add(row.GetUInt(0), BuildAreaGroupName(row, 1, 6)));
                        Load("ItemDisplayInfo.dbc", 0, 5, ItemDisplayInfoStore);
                        Load("MailTemplate.dbc", row =>
                        {
                            var subject = row.GetString(1);
                            var body = row.GetString(2);
                            var name = string.IsNullOrEmpty(subject) ? body.TrimToLength(50) : subject;
                            MailTemplateStore.Add(row.GetUInt(0), name.Replace("\n", ""));
                        });
                        Load("LFGDungeons.dbc", 0, 1, LFGDungeonStore);
                        Load("ItemSet.dbc", 0, 1, ItemSetStore);
                        Load("DungeonEncounter.dbc", 0, 5, DungeonEncounterStore);
                        Load("HolidayNames.dbc", 0, 1, HolidayNamesStore);
                        Load("Holidays.dbc", row =>
                        {
                            var id = row.GetUInt(0);
                            var nameId = row.GetUInt(49);
                            if (HolidayNamesStore.TryGetValue(nameId, out var name))
                                HolidaysStore[id] = name;
                            else
                                HolidaysStore[id] = "Holiday " + id;
                        });
                        Load("WorldSafeLocs.dbc", 0, 5, WorldSafeLocsStore);
                        Load("BattlemasterList.dbc", 0, 11, BattlegroundStore);
                        Load("Achievement_Criteria.dbc", 0, 10, AchievementCriteriaStore);
                        Load("Item.dbc", row =>
                        {
                            var id = row.GetUInt(0);
                            var displayId = row.GetUInt(5);
                            if (ItemDisplayInfoStore.TryGetValue(displayId, out var name))
                                ItemDbcStore[id] = name;
                            else
                                ItemDbcStore[id] = "Item " + id;
                        });
                        Load("LockType.dbc", 0, 1, LockTypeStore);
                        LoadAndRegister("SpellCastTimes.dbc", "SpellCastTimeParameter", 0, row => GetCastTimeDescription(row.GetInt(1), row.GetInt(2), row.GetInt(3)));
                        LoadAndRegister("SpellDuration.dbc", "SpellDurationParameter", 0, row => GetDurationTimeDescription(row.GetInt(1), row.GetInt(2), row.GetInt(3)));
                        LoadAndRegister("SpellRange.dbc", "SpellRangeParameter", 0, 6);
                        LoadAndRegister("SpellRadius.dbc", "SpellRadiusParameter", 0, row => GetRadiusDescription(row.GetFloat(1), row.GetFloat(2), row.GetFloat(3)));
                        break;
                    }
                    case DBCVersions.MOP_18414:
                    {
                        store.cataSpellService.Load(dbcSettingsProvider.GetSettings().Path);
                        max = 49;
                        var fileData = new Dictionary<long, string>();
                        Load("Achievement_Criteria.dbc", 0, 10, AchievementCriteriaStore);
                        Load("FileData.dbc", 0, 1, fileData);
                        Load("AreaTrigger.dbc", row => AreaTriggerStore.Add(row.GetInt(0), $"Area trigger"));
                        Load("BattlemasterList.dbc", 0, 19, BattlegroundStore);
                        Load("SkillLine.dbc", 0, 2, SkillStore);
                        Load("Faction.dbc", row =>
                        {
                            var faction = new Faction(row.GetUShort(0), row.GetString(23));
                            Factions.Add(faction);
                            FactionStore[faction.FactionId] = faction.Name;
                        });
                        Load("FactionTemplate.dbc", row =>
                        {
                            var template = new FactionTemplate()
                            {
                                TemplateId = row.GetUInt(0),
                                Faction = row.GetUShort(1),
                                Flags = row.GetUShort(2),
                                FactionGroup = (FactionGroupMask)row.GetUShort(3),
                                FriendGroup = (FactionGroupMask)row.GetUShort(4),
                                EnemyGroup = (FactionGroupMask)row.GetUShort(5)
                            };
                            FactionTemplates.Add(template);
                            FactionTemplateStore[row.GetUInt(0)] = row.GetUInt(1);
                        });
                        Load("CurrencyTypes.dbc", 0, 2, CurrencyTypeStore);
                        Load("Spell.dbc", 0, 1, SpellStore);
                        Load("Movie.dbc", row => MovieStore.Add(row.GetInt(0), fileData.GetValueOrDefault(row.GetInt(3)) ?? "Unknown movie"));
                        Load("Map.dbc", row =>
                        {
                            var map = new MapEntry()
                            {
                                Id = row.GetUInt(0),
                                Name = row.GetString(5),
                                Directory = row.GetString(1),
                                Type = (InstanceType)row.GetUInt(2),
                            };
                            Maps.Add(map);
                        });
                        Load("Achievement.dbc", 0, 4, AchievementStore);
                        Load("AreaTable.dbc", row =>
                        {
                            var entry = new AreaEntry()
                            {
                                Id = row.GetUInt(0),
                                MapId = row.GetUInt(1),
                                ParentAreaId = row.GetUInt(2),
                                Flags1 = row.GetUInt(4),
                                Flags2 = row.GetUInt(5),
                                Name = row.GetString(13)
                            };
                            Areas.Add(entry);
                        });
                        FillMapAreas();
                        Load("ChrClasses.dbc", 0, 3, ClassStore);
                        Load("ChrRaces.dbc", 0, 14, RaceStore);
                        Load("Difficulty.dbc", 0, 11, DifficultyStore);
                        Load("Emotes.dbc", row =>
                        {
                            var proc = row.GetUInt(4);
                            if (proc == 0)
                                EmoteOneShotStore.Add(row.GetUInt(0), row.GetString(1));
                            else if (proc == 2)
                                EmoteStateStore.Add(row.GetUInt(0), row.GetString(1));
                            EmoteStore.Add(row.GetUInt(0), row.GetString(1));
                        });
                        Load("EmotesText.dbc", 0, 1, TextEmoteStore);
                        Load("Item-sparse.db2", 0, 100, ItemStore);
                        Load("Phase.dbc", 0, 1, PhaseStore);
                        Load("SoundEntries.dbc", 0, 2, SoundStore);
                        Load("SpellFocusObject.dbc", 0, 1, SpellFocusObjectStore);
                        Load("QuestInfo.dbc", 0, 1, QuestInfoStore);
                        Load("CharTitles.dbc", 0, 2, CharTitleStore);
                        Load("CreatureModelData.dbc", 0, 2, CreatureModelDataStore);
                        Load("CreatureDisplayInfo.dbc", 0, 1, CreatureDisplayInfoStore);
                        Load("GameObjectDisplayInfo.dbc", 0, 1, GameObjectDisplayInfoStore);
                        Load("Languages.dbc", 0, 1, LanguageStore);
                        Load("QuestSort.dbc", 0, 1, QuestSortStore);
                        Load("ItemExtendedCost.dbc", row => ExtendedCostStore.Add(row.GetInt(0), GenerateCostDescription(row.GetInt(1), row.GetInt(2), row.GetInt(4))));
                        Load("TaxiNodes.dbc", 0, 5, TaxiNodeStore);
                        Load("TaxiPath.dbc",  row => TaxiPathsStore.Add(row.GetUInt(0), (row.GetInt(1), row.GetInt(2))));
                        Load("SpellItemEnchantment.dbc", 0, 11, SpellItemEnchantmentStore);
                        Load("AreaGroup.dbc",  row => AreaGroupStore.Add(row.GetUInt(0), BuildAreaGroupName(row, 1, 6)));
                        Load("ItemDisplayInfo.dbc", 0, 5, ItemDisplayInfoStore);
                        Load("MailTemplate.dbc", row =>
                        {
                            var subject = row.GetString(1);
                            var body = row.GetString(2);
                            var name = string.IsNullOrEmpty(subject) ? body.TrimToLength(50) : subject;
                            MailTemplateStore.Add(row.GetUInt(0), name.Replace("\n", ""));
                        });
                        Load("LFGDungeons.dbc", 0, 1, LFGDungeonStore);
                        Load("ItemSet.dbc", 0, 1, ItemSetStore);
                        Load("DungeonEncounter.dbc", 0, 5, DungeonEncounterStore);
                        Load("HolidayNames.dbc", 0, 1, HolidayNamesStore);
                        Load("Holidays.dbc", row =>
                        {
                            var id = row.GetUInt(0);
                            var nameId = row.GetUInt(49);
                            if (HolidayNamesStore.TryGetValue(nameId, out var name))
                                HolidaysStore[id] = name;
                            else
                                HolidaysStore[id] = "Holiday " + id;
                        });
                        Load("WorldSafeLocs.dbc", 0, 6, WorldSafeLocsStore);
                        Load("Item.dbc", row =>
                        {
                            var id = row.GetUInt(0);
                            var displayId = row.GetUInt(5);
                            if (ItemDisplayInfoStore.TryGetValue(displayId, out var name))
                                ItemDbcStore[id] = name;
                            else
                                ItemDbcStore[id] = "Item " + id;
                        });
                        Load("LockType.dbc", 0, 1, LockTypeStore);
                        Load("Vignette.dbc", 0, 1, VignetteStore);
                        LoadAndRegister("SpellCastTimes.dbc", "SpellCastTimeParameter", 0, row => GetCastTimeDescription(row.GetInt(1), row.GetInt(2), row.GetInt(3)));
                        LoadAndRegister("SpellDuration.dbc", "SpellDurationParameter", 0, row => GetDurationTimeDescription(row.GetInt(1), row.GetInt(2), row.GetInt(3)));
                        LoadAndRegister("SpellRange.dbc", "SpellRangeParameter", 0, 6);
                        LoadAndRegister("SpellRadius.dbc", "SpellRadiusParameter", 0, row => GetRadiusDescription(row.GetFloat(1), row.GetFloat(2), row.GetFloat(4)));
                        break;
                    }
                    case DBCVersions.LEGION_26972:
                    {
                        store.legionSpellService.Load(dbcSettingsProvider.GetSettings().Path);
                        max = 42;
                        Load("CriteriaTree.db2", 0, 1, AchievementCriteriaStore);
                        Load("AreaTrigger.db2", row => AreaTriggerStore.Add(row.GetInt(14), $"Area trigger"));
                        Load("AreaTable.db2", row =>
                        {
                            var entry = new AreaEntry()
                            {
                                Id = row.GetUInt(0),
                                MapId = row.GetUInt(5),
                                ParentAreaId = row.GetUInt(6),
                                Flags1 = row.GetUInt(3, 0),
                                Flags2 = row.GetUInt(3, 1),
                                Name = row.GetString(2)
                            };
                            Areas.Add(entry);
                        });
                        Load("Map.db2", row =>
                        {
                            var map = new MapEntry()
                            {
                                Id = row.GetUInt(0),
                                Name = row.GetString(2),
                                Directory = row.GetString(1),
                                Type = (InstanceType)row.GetUInt(17),
                            };
                            Maps.Add(map);
                        });
                        FillMapAreas();
                        LoadLegionAreaGroup(AreaGroupStore);
                        Load("BattlemasterList.db2", 0, 1, BattlegroundStore);
                        Load("CurrencyTypes.db2", 0, 1, CurrencyTypeStore);
                        Load("DungeonEncounter.db2", 6, 0, DungeonEncounterStore);
                        Load("Difficulty.db2", 0, 1, DifficultyStore);
                        Load("ItemSparse.db2", 0, 2, ItemStore);
                        Load("ItemExtendedCost.db2", row =>
                        {
                            var id = row.GetUInt(0);
                            StringBuilder desc = new StringBuilder();
                            for (int i = 0; i < 5; ++i)
                            {
                                var count = row.GetUInt(2, i);
                                var currency = row.GetUInt(5, i);
                                var item = row.GetUInt(1, i);
                                var itemsCount = row.GetUShort(3, i);

                                if (currency != 0 && count != 0)
                                {
                                    if (CurrencyTypeStore.TryGetValue(currency, out var currencyName))
                                        desc.Append($"{count} x {currencyName}, ");
                                    else
                                        desc.Append($"{count} x Currency {currency}, ");
                                }
                                if (itemsCount != 0 && item != 0)
                                {
                                    if (!ItemStore.TryGetValue(item, out var itemName))
                                        itemName = "item " + item;
                                    
                                    if (itemsCount == 1)
                                        desc.Append($"{itemName}, ");
                                    else
                                        desc.Append($"{itemsCount} x {itemName}, ");
                                }
                            }
                            var arenaRating = row.GetUShort(4);
                            if (arenaRating != 0)
                            {
                                desc.Append($"min arena rating {arenaRating}");
                            }
                            ExtendedCostStore.Add(id, desc.ToString());
                        });
                        Load("ItemSet.db2", 0, 1, ItemSetStore);
                        Load("LFGDungeons.db2", 0, 1, LFGDungeonStore);
                        Load("chrRaces.db2", 30, 2, RaceStore);
                        Load("achievement.db2", row =>  AchievementStore.Add(row.GetInt(12), row.GetString(0)));
                        Load("spell.db2", row => SpellStore.Add(row.Key, row.GetString(1)));
                        Load("chrClasses.db2", 19, 1, ClassStore);
                        
                        Load("Emotes.db2", row =>
                        {
                            var id = row.Key;
                            var name = row.GetString(2);
                            var proc = row.GetInt(6);
                            if (proc == 0)
                                EmoteOneShotStore.Add(id, name);
                            else if (proc == 2)
                                EmoteStateStore.Add(id, name);
                            EmoteStore.Add(id, name);
                        });
                        
                        Load("EmotesText.db2", 0, 1, TextEmoteStore);
                        Load("HolidayNames.db2", 0, 1, HolidayNamesStore);
                        Load("Holidays.db2", row =>
                        {
                            var id = row.GetUInt(0);
                            var nameId = row.GetUInt(9);
                            if (HolidayNamesStore.TryGetValue(nameId, out var name))
                                HolidaysStore[id] = name;
                            else
                                HolidaysStore[id] = "Holiday " + id;
                        });
                        Load("Languages.db2", 1, 0, LanguageStore);
                        Load("MailTemplate.DB2", row =>
                        {
                            var body = row.GetString(1);
                            var name = body.TrimToLength(50);
                            MailTemplateStore.Add(row.GetUInt(0), name.Replace("\n", ""));
                        });
                        Load("Faction.db2", row =>
                        {
                            var faction = new Faction(row.GetUShort(3), row.GetString(1));
                            Factions.Add(faction);
                            FactionStore[faction.FactionId] = faction.Name;
                        });
                        Load("FactionTemplate.db2", row =>
                        {
                            var template = new FactionTemplate()
                            {
                                TemplateId = row.Key,
                                Faction = row.GetUShort(1),
                                Flags = row.GetUShort(2),
                                FactionGroup = (FactionGroupMask)row.GetUShort(5),
                                FriendGroup = (FactionGroupMask)row.GetUShort(6),
                                EnemyGroup = (FactionGroupMask)row.GetUShort(7)
                            };
                            FactionTemplates.Add(template);
                            FactionTemplateStore.Add(row.GetInt(0), row.GetUShort(1));
                        });
                        // Load("Phase.db2", 1, 0, PhaseStore); // no names in legion :(
                        Load("SoundKitName.db2", 0, 1, SoundStore);
                        Load("SpellFocusObject.db2", 0, 1, SpellFocusObjectStore);
                        Load("QuestInfo.db2", 0, 1, QuestInfoStore);
                        Load("QuestSort.db2", 0, 1, QuestSortStore);
                        Load("CharTitles.db2", 0, 1, CharTitleStore);
                        Load("SkillLine.db2", 0, 1, SkillStore);
                        Load("LockType.db2", 4, 0, LockTypeStore);
                        Load("CreatureDisplayInfo.db2", row => CreatureDisplayInfoStore.Add(row.GetInt(0), row.GetUShort(2)));
                        LoadAndRegister("SpellCastTimes.db2", "SpellCastTimeParameter", 0, row => GetCastTimeDescription(row.GetInt(1), row.GetInt(3), row.GetInt(2)));
                        LoadAndRegister("SpellDuration.db2", "SpellDurationParameter", 0, row => GetDurationTimeDescription(row.GetInt(1), row.GetInt(3), row.GetInt(2)));
                        LoadAndRegister("SpellRange.db2", "SpellRangeParameter", 0, 1);
                        LoadAndRegister("SpellRadius.db2", "SpellRadiusParameter", 0, row => GetRadiusDescription(row.GetFloat(1), row.GetFloat(2), row.GetFloat(4)));
                        Load("SpellItemEnchantment.db2", 0, 1, SpellItemEnchantmentStore);
                        Load("TaxiNodes.db2",  0, 1, TaxiNodeStore);
                        Load("TaxiPath.db2",  row => TaxiPathsStore.Add(row.GetInt(2), (row.GetUShort(0), row.GetUShort(1))));
                        Load("SceneScriptPackage.db2", 0, 1, SceneStore);
                        Load("BattlePetSpecies.db2", 8, 2, BattlePetSpeciesIdStore);
                        Load("BattlePetAbility.db2", 0, 1, BattlePetAbilityStore);
                        Load("Scenario.db2", 0, 1, ScenarioStore);
                        Load("Vignette.db2", 0, 1, VignetteStore);
                        Load("GarrClassSpec.db2", 7, 0, GarrisonClassSpecStore);
                        Load("GarrTalent.db2", 7, 0, GarrisonTalentStore);
                        Load("GarrBuilding.db2", row =>
                        {
                            var id = row.Key;
                            var allyName = row.GetString(1);
                            var hordeName = row.GetString(2);
                            if (allyName == hordeName)
                                GarrisonBuildingStore[id] = allyName;
                            else
                                GarrisonBuildingStore[id] = $"{allyName} / {hordeName}";
                        });
                        Load("ScenarioStep.db2", row =>
                        {
                            var stepId = row.Key;
                            var description = row.GetString(0);
                            var name = row.GetString(1);
                            var scenarioId = row.GetUInt(3);
                            var stepIndex = row.GetUInt(6);
                            ScenarioStepStore[stepId] = name;
                            if (!ScenarioToStepStore.TryGetValue(scenarioId, out var scenarioSteps))
                                scenarioSteps = ScenarioToStepStore[scenarioId] = new();
                            scenarioSteps[stepIndex] = stepId;
                        });
                        Load("ChrSpecialization.db2", row =>
                        {
                            var specId = row.Key;
                            var name = row.GetString(0);
                            var classId = row.GetUInt(4);
                            if (ClassStore.TryGetValue(classId, out var className))
                                CharSpecializationStore.Add(specId, $"{className} - {name}");
                            else
                                CharSpecializationStore.Add(specId, $"{name}");
                        });
                        Load("AdventureJournal.db2", 0, 1, AdventureJournalStore);
                        break;
                    }
                    case DBCVersions.SHADOWLANDS_41079:
                    {
                        max = 19;
                        Load("AreaTrigger.db2", string.Empty, AreaTriggerStore);
                        Load("SpellName.db2", "Name_lang", SpellStore);
                        Load("Achievement.db2", "Title_lang", AchievementStore);
                        Load("AreaTable.db2", "AreaName_lang", AreaStore);
                        Load("ChrClasses.db2", "Name_lang", ClassStore);
                        Load("ChrRaces.db2", "Name_lang", RaceStore);
                        LoadDB2("Emotes.db2", row =>
                        {
                            var proc = row.FieldAs<uint>("EmoteSpecProc");
                            var name = row.FieldAs<string>("EmoteSlashCommand");
                            if (proc == 0)
                                EmoteOneShotStore.Add(row.ID, name);
                            else if (proc == 2)
                                EmoteStateStore.Add(row.ID, name);
                            EmoteStore.Add(row.ID, name);
                        });
                        Load("EmotesText.db2", "Name", TextEmoteStore);
                        Load("ItemSparse.db2", "Display_lang", ItemStore);
                        Load("Languages.db2", "Name_lang", LanguageStore);
                        Load("Map.db2", "Directory", MapDirectoryStore);
                        Load("Map.db2", "MapName_lang", MapStore);
                        Load("Faction.db2", "Name_lang", FactionStore);
                        Load("FactionTemplate.db2", "Faction", FactionTemplateStore);
                        Load("SceneScriptPackage.db2", "Name", SceneStore);
                        Load("SpellFocusObject.db2", "Name_lang", SpellFocusObjectStore);
                        Load("QuestInfo.db2", "InfoName_lang", QuestInfoStore);
                        Load("CharTitles.db2", "Name_lang", CharTitleStore);
                        Load("QuestSort.db2", "SortName_lang", QuestSortStore);
                        Load("TaxiNodes.db2",  "Name_lang", TaxiNodeStore);
                        LoadDB2("TaxiPath.db2",  row => TaxiPathsStore.Add(row.ID, (row.Field<int>("FromTaxiNode"), row.Field<int>("ToTaxiNode"))));
                        break;
                    }
                    default:
                        return;
                }

                switch (dbcSettingsProvider.GetSettings().DBCLocale)
                {
                    case DBCLocales.LANG_enUS:
                        Validate(SpellStore, 1, "Word of Recall (OLD)");
                        break;
                    case DBCLocales.LANG_frFR:
                        Validate(SpellStore, 1, "Mot de rappel (OLD)");
                        break;
                    default:
                        return;
                }
            }

            private void FillMapAreas()
            {
                var mapById = Maps.ToDictionary(x => x.Id, x => x);
                var areasById = Areas.ToDictionary(x => x.Id, x => x);
                foreach (var area in Areas)
                {
                    area.Map = mapById.TryGetValue(area.MapId, out var map) ? map : null;
                    if (area.ParentAreaId != 0)
                        area.ParentArea = areasById.TryGetValue(area.ParentAreaId, out var parentArea) ? parentArea : null;
                    
                    AreaStore.Add(area.Id, area.Name);
                }

                foreach (var map in Maps)
                {
                    MapStore.Add(map.Id, map.Name);
                    MapDirectoryStore.Add(map.Id, map.Directory);
                }
            }

            private void LoadLegionAreaGroup(Dictionary<long, string> areaGroupStore)
            {
                Dictionary<ushort, List<ushort>> areaGroupToArea = new Dictionary<ushort, List<ushort>>();
                Load("AreaGroupMember.db2", row =>
                {
                    var areaId = row.GetUShort(1);
                    var areaGroup = row.GetUShort(2);
                    if (!areaGroupToArea.TryGetValue(areaGroup, out var list))
                        list = areaGroupToArea[areaGroup] = new();
                    list.Add(areaId);
                });
                foreach (var (group, list) in areaGroupToArea)
                    areaGroupStore.Add(group, BuildAreaGroupName(list));
            }

            private string GetLockDescription(Func<int, long> getLockKeyType, Func<int, long> getLockProperty, Func<int, long> getSkill)
            {
                for (int i = 0; i < 8; ++i)
                {
                    var type = (LockKeyType)getLockKeyType(i);
                    
                    if (type == LockKeyType.None)
                        continue;
                    
                    var lockProperty = getLockProperty(i);
                    var skill = getSkill(i);

                    if (type == LockKeyType.Item)
                    {
                        
                    }
                    else if (type == LockKeyType.Skill)
                    {
                        
                    }
                    else if (type == LockKeyType.Spell)
                    {
                        
                    }
                }

                return "";
            }

            private string GetRadiusDescription(float @base, float perLevel, float max)
            {
                if (perLevel == 0)
                    return @base + " yd";
                if (max == 0)
                    return $"{@base} yd + {perLevel} yd/level";
                return $"min({max} yd, {@base} yd + {perLevel} yd/level)";
            }

            private string GetCastTimeDescription(int @base, int perLevel, int min)
            {
                if (@base == 0 && perLevel == 0 && min == 0)
                    return "Instant";
                if (perLevel == 0)
                    return @base.ToPrettyDuration();
                if (min == 0)
                    return $"{@base.ToPrettyDuration()} + {perLevel.ToPrettyDuration()}/level";
                return $"max({min.ToPrettyDuration()}, {@base.ToPrettyDuration()} + {perLevel.ToPrettyDuration()}/level)";
            }

            private string GetDurationTimeDescription(int @base, int perLevel, int max)
            {
                if (@base == -1)
                    return "Infinite";
                if (perLevel == 0)
                    return @base.ToPrettyDuration();
                if (max == 0)
                    return $"{@base.ToPrettyDuration()} + {perLevel.ToPrettyDuration()}/level";
                return $"min({max.ToPrettyDuration()}, {@base.ToPrettyDuration()} + {perLevel.ToPrettyDuration()}/level)";
            }
            
            private string BuildAreaGroupName(IReadOnlyList<ushort> areas)
            {
                if (areas.Count == 1)
                {
                    if (AreaStore.TryGetValue(areas[0], out var name))
                        return name;
                    return "Area " + areas[0];
                }
                
                return string.Join(", ", areas.Select(area => AreaStore.TryGetValue(area, out var name) ? name : "Area " + area));
            }
            
            private string BuildAreaGroupName(IDbcIterator row, int start, int count)
            {
                for (int i = start; i < start + count; ++i)
                {
                    var id = row.GetUInt(i);
                    if (id == 0)
                    {
                        count = i - start;
                        break;
                    }
                }

                if (count == 1)
                {
                    if (AreaStore.TryGetValue(row.GetUInt(start), out var name))
                        return name;
                    return "Area " + row.GetUInt(start);
                }
                
                StringBuilder sb = new();
                for (int i = start; i < start + count; ++i)
                {
                    if (AreaStore.TryGetValue(row.GetUInt(start), out var name))
                        sb.Append(name);
                    else
                        sb.Append("Area " + row.GetUInt(i));
                    
                    if (i != start + count - 1)
                        sb.Append(", ");
                }

                return sb.ToString();
            }

            private string GenerateCostDescription(int honor, int arenaPoints, int item)
            {
                if (honor != 0 && arenaPoints == 0 && item == 0)
                    return honor + " honor";
                if (honor == 0 && arenaPoints != 0 && item == 0)
                    return arenaPoints + " arena points";
                if (honor == 0 && arenaPoints == 0 && item != 0)
                    return item + " item";
                if (item == 0)
                    return honor + " honor, " + arenaPoints + " arena points";
                return honor + " honor, " + arenaPoints + " arena points, " + item + " item";
            }

            private void Validate(Dictionary<long,string> dict, int id, string expectedName)
            {
                if (dict.TryGetValue(id, out var realName) && realName == expectedName)
                    return;

                var settings = dbcSettingsProvider.GetSettings();

                store.messageBoxService.ShowDialog(new MessageBoxFactory<bool>()
                    .SetIcon(MessageBoxIcon.Error)
                    .SetTitle("Invalid DBC")
                    .SetMainInstruction("Invalid DBC path")
                    .SetContent(
                        $"In specified path, there is no DBC for version {settings.DBCVersion}. Ensure the path contains Spell.dbc or Spell.db2 file.\n\nPath: {settings.Path}")
                    .WithOkButton(false)
                    .Build());
                throw new Exception("Invalid DBC!");
            }
        }

        private void InvokeChangedSpells()
        {
            Changed?.Invoke(this);
        }

        private IDbcSpellService spellServiceImpl;
        public bool Exists(uint spellId) => spellServiceImpl.Exists(spellId);
        public int SpellCount => spellServiceImpl.SpellCount;
        public uint GetSpellId(int index) => spellServiceImpl.GetSpellId(index);
        public T GetAttributes<T>(uint spellId) where T : unmanaged, Enum => spellServiceImpl.GetAttributes<T>(spellId);
        public uint? GetSkillLine(uint spellId) => spellServiceImpl.GetSkillLine(spellId);
        public uint? GetSpellFocus(uint spellId) => spellServiceImpl.GetSpellFocus(spellId);
        public TimeSpan? GetSpellCastingTime(uint spellId) => spellServiceImpl.GetSpellCastingTime(spellId);
        public TimeSpan? GetSpellDuration(uint spellId) => spellServiceImpl.GetSpellDuration(spellId);
        public TimeSpan? GetSpellCategoryRecoveryTime(uint spellId) => spellServiceImpl.GetSpellCategoryRecoveryTime(spellId);
        public string GetName(uint spellId) => spellServiceImpl.GetName(spellId);
        public event Action<ISpellService>? Changed;
        public string? GetDescription(uint spellId) => spellServiceImpl.GetDescription(spellId);
        public int GetSpellEffectsCount(uint spellId) => spellServiceImpl.GetSpellEffectsCount(spellId);
        public SpellAuraType GetSpellAuraType(uint spellId, int effectIndex) => spellServiceImpl.GetSpellAuraType(spellId, effectIndex);
        public SpellEffectType GetSpellEffectType(uint spellId, int index) => spellServiceImpl.GetSpellEffectType(spellId, index);
        public SpellTargetFlags GetSpellTargetFlags(uint spellId) => spellServiceImpl.GetSpellTargetFlags(spellId);
        public (SpellTarget, SpellTarget) GetSpellEffectTargetType(uint spellId, int index) => spellServiceImpl.GetSpellEffectTargetType(spellId, index);
        public uint GetSpellEffectMiscValueA(uint spellId, int index) => spellServiceImpl.GetSpellEffectMiscValueA(spellId, index);
        public uint GetSpellEffectTriggerSpell(uint spellId, int index) => spellServiceImpl.GetSpellEffectTriggerSpell(spellId, index);
    }

    internal class FactionParameter : ParameterNumbered
    {
        public FactionParameter(Dictionary<long, string> factionStore, Dictionary<long, long> factionTemplateStore)
        {
            Items = new Dictionary<long, SelectOption>();
            foreach (var factionTemplate in factionTemplateStore)
            {
                if (factionStore.TryGetValue(factionTemplate.Value, out var factionName))
                    Items.Add(factionTemplate.Key, new SelectOption(factionName));
                else
                    Items.Add(factionTemplate.Key, new SelectOption("unknown name"));
            }
        }
    }

    internal class CreatureModelParameter : ParameterNumbered
    {
        public CreatureModelParameter(Dictionary<long, string> creatureModelData, Dictionary<long, long> creatureDisplayInfo)
        {
            Items = new Dictionary<long, SelectOption>();
            foreach (var displayInfo in creatureDisplayInfo)
            {
                if (creatureModelData.TryGetValue(displayInfo.Value, out var modelPath))
                    Items.Add(displayInfo.Key, new SelectOption(GetFileName(modelPath), modelPath));
                else
                    Items.Add(displayInfo.Key, new SelectOption("unknown model"));
            }
        }
        
        private string GetFileName(string s)
        {
            int indexOf = Math.Max(s.LastIndexOf('\\'), s.LastIndexOf('/'));
            return indexOf == -1 ? s : s.Substring(indexOf + 1);
        }
    }

    public class DbcParameter : ParameterNumbered
    {
        public DbcParameter()
        {
            Items = new();
        }
        
        public DbcParameter(Dictionary<long, string> storage)
        {
            Items = new Dictionary<long, SelectOption>();
            foreach (var (key, value) in storage)
                Items.Add(key, new SelectOption(value));
        }

        public bool AllowUnknownItems => true;
    }

    public class WoWToolsParameter : Parameter, ICustomPickerParameter<long>
    {
        private readonly string dbcName;
        private readonly IWindowManager windowManager;
        private readonly string buildString;

        public override bool HasItems => true;
        public bool AllowUnknownItems => true;

        public WoWToolsParameter(string dbcName, 
            ICurrentCoreVersion currentCoreVersion, 
            IWindowManager windowManager)
        {
            this.dbcName = dbcName;
            this.windowManager = windowManager;
            var version = currentCoreVersion.Current.Version;
            var build = version.Build == 18414 ? 18273 : version.Build;
            buildString = $"{version.Major}.{version.Minor}.{version.Patch}.{build}";
        }
        
        public Task<(long, bool)> PickValue(long value)
        {
            windowManager.OpenUrl($"https://wow.tools/dbc/?dbc={dbcName}&build={buildString}#page=1&colFilter[0]=exact%3A{value}");
            return Task.FromResult((0L, false));
        }
    }

    public class DbcParameterWowTools : DbcParameter, ICustomPickerParameter<long>
    {
        private readonly string dbcName;
        private readonly IWindowManager windowManager;
        private readonly string buildString;
        
        public override bool HasItems => true;
        
        public DbcParameterWowTools(Dictionary<long, string> storage, 
            string dbcName, 
            ICurrentCoreVersion currentCoreVersion, 
            IWindowManager windowManager) : base(storage)
        {
            this.dbcName = dbcName;
            this.windowManager = windowManager;
            var version = currentCoreVersion.Current.Version;
            var build = version.Build == 18414 ? 18273 : version.Build;
            buildString = $"{version.Major}.{version.Minor}.{version.Patch}.{build}";
        }
        
        public Task<(long, bool)> PickValue(long value)
        {
            windowManager.OpenUrl($"https://wow.tools/dbc/?dbc={dbcName}&build={buildString}#page=1&colFilter[0]=exact%3A{value}");
            return Task.FromResult((0L, false));
        }
    }
    
    public class DbcParameterWithPicker<T> : DbcParameter, ICustomPickerParameter<long>
    {
        private readonly ITabularDataPicker dataPicker;
        private readonly string dbc;
        private readonly Func<T, long> getId;
        private readonly Func<IReadOnlyList<T>> getListOf;
        private readonly Func<T, string, bool> filter;
        private readonly ITabularDataColumn[] columns;

        public DbcParameterWithPicker(ITabularDataPicker dataPicker,
            Dictionary<long, string> storage,
            string dbc,
            Func<T, long> getId,
            Func<IReadOnlyList<T>> getListOf,
            Func<T, string, bool> filter,
            params ITabularDataColumn?[] columns) : base(storage)
        {
            this.dataPicker = dataPicker;
            this.dbc = dbc;
            this.getId = getId;
            this.getListOf = getListOf;
            this.filter = filter;
            this.columns = columns.Where(c => c != null).Cast<ITabularDataColumn>().ToArray();
        }
        
        public async Task<(long, bool)> PickValue(long value)
        {
            var result = await dataPicker.PickRow(new TabularDataBuilder<T>()
                .SetData(getListOf().AsIndexedCollection())
                .SetTitle($"Pick {dbc}")
                .SetFilter(filter)
                .SetColumns(columns)
                .Build());

            if (result == null)
                return (0, false);
            
            return (getId(result), true);
        }
    }
    
    public class BattlePetSpeciesParameter : ParameterNumbered
    {
        private readonly DbcStore dbcStore;
        private readonly IParameterFactory parameterFactory;
        private readonly IParameter<long> creatures;

        public BattlePetSpeciesParameter(DbcStore dbcStore, IParameterFactory parameterFactory, IParameter<long> creatures)
        {
            this.dbcStore = dbcStore;
            this.parameterFactory = parameterFactory;
            this.creatures = creatures;
            Items = new Dictionary<long, SelectOption>();
            Refresh();

            parameterFactory.OnRegister().SubscribeAction(p =>
            {
                if (p == creatures)
                    Refresh();
            });
        }

        private void Refresh()
        {
            Items!.Clear();
            foreach (var (key, value) in dbcStore.BattlePetSpeciesIdStore)
            {
                if (creatures.Items != null && creatures.Items.TryGetValue(value, out var petName))
                    Items!.Add(key, new SelectOption(petName.Name + " (" + value + ")"));
                else
                    Items!.Add(key, new SelectOption("Creature " + value));
            }
        }
    }

    public class LanguageParameter : DbcParameter
    {
        public LanguageParameter(Dictionary<long, string> storage) : base(storage)
        {
            Items!.Add(0, new SelectOption("Universal"));
        }
    }

    public class ZoneOrQuestSortParameter : DbcParameter
    {
        public ZoneOrQuestSortParameter(Dictionary<long, string> zones, Dictionary<long, string> questSorts) : base(zones)
        {
            foreach (var pair in questSorts)
                Items!.Add(-pair.Key, new SelectOption(pair.Value));
        }
    }

    public class TaxiPathParameter : DbcParameter
    {
        public TaxiPathParameter(Dictionary<long, (int, int)> taxiPathsStore, Dictionary<long, string> taxiNodes)
        {
            foreach (var path in taxiPathsStore)
            {
                var from = taxiNodes.TryGetValue(path.Value.Item1, out var fromName) ? fromName : "unknown";
                var to = taxiNodes.TryGetValue(path.Value.Item2, out var toName) ? toName : "unknown";
                Items!.Add(path.Key, new SelectOption($"{from} -> {to}"));
            }
        }
    }
    
    public class DbcFileParameter : Parameter
    {
        public DbcFileParameter(Dictionary<long, string> storage)
        {
            Items = new Dictionary<long, SelectOption>();
            foreach (int key in storage.Keys)
                Items.Add(key, new SelectOption(GetFileName(storage[key]), storage[key]));
        }

        private string GetFileName(string s)
        {
            int indexOf = s.LastIndexOf('\\');
            return indexOf == -1 ? s : s.Substring(indexOf + 1);
        }
    }
    
    public class DbcMaskParameter : FlagParameter
    {
        public DbcMaskParameter(Dictionary<long, string> storage, int offset)
        {
            Items = new Dictionary<long, SelectOption>();
            foreach (int key in storage.Keys)
                Items.Add(1L << (key + offset), new SelectOption(storage[key]));
        }
    }

    public class RaceMaskParameter : FlagParameter
    {
        private CharacterRaces alliance;
        private CharacterRaces horde;
        private CharacterRaces all;

        public override string ToString(long value)
        {
            if ((long)all == value)
                return "Any race";
            if ((long)alliance == value)
                return "Alliance";
            if ((long)horde == value)
                return "Horde";
            return base.ToString(value);
        }

        private static bool IsPowerOfTwo(ulong x)
        {
            return (x != 0) && ((x & (x - 1)) == 0);
        }
    
        public RaceMaskParameter(CharacterRaces allowableRaces)
        {
            Items = new Dictionary<long, SelectOption>();

            alliance = allowableRaces & CharacterRaces.AllAlliance;
            horde = allowableRaces & CharacterRaces.AllHorde;
            all = allowableRaces;
            if (alliance != CharacterRaces.None)
                Items.Add((long)alliance, new SelectOption("Alliance"));
            
            if (horde != CharacterRaces.None)
                Items.Add((long)horde, new SelectOption("Horde"));
            
            foreach (CharacterRaces race in Enum.GetValues<CharacterRaces>())
            {
                if (IsPowerOfTwo((ulong)race) && allowableRaces.HasFlagFast(race))
                {
                    Items.Add((long)race, new SelectOption(race.ToString().ToTitleCase()));
                }
            }
        }
    }
    
}