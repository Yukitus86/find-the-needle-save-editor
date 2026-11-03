namespace FindTheNeedleEditor.Core;

/// <summary>Known ids from the game (taken from saves and settings.cfg) with readable names.
/// Unknown ids still work – they are shown with a generated name.</summary>
public static class GameData
{
    public enum TechGroup { Tools, Body, Machines, Power, Belts, Scanning, Bales, Storage, Drones, Yard }

    // id -> (German, English, group)
    static readonly Dictionary<string, (string De, string En, TechGroup G)> Techs = new()
    {
        ["yard"] = ("Hof", "Yard", TechGroup.Yard),
        ["yard_space"] = ("Hof-Fläche", "Yard space", TechGroup.Yard),
        ["build_reach"] = ("Bau-Reichweite", "Build reach", TechGroup.Yard),
        ["wall"] = ("Wand", "Wall", TechGroup.Yard),
        ["deck"] = ("Plattform", "Deck", TechGroup.Yard),
        ["haystairs"] = ("Heu-Treppe", "Hay stairs", TechGroup.Yard),
        ["haylift"] = ("Heu-Lift", "Hay lift", TechGroup.Yard),
        ["steel_saving"] = ("Stahl sparen", "Steel saving", TechGroup.Yard),

        ["hand_carry"] = ("Tragen von Hand", "Hand carry", TechGroup.Tools),
        ["sand_shovel"] = ("Sandschaufel", "Sand shovel", TechGroup.Tools),
        ["toy_shovel_size"] = ("Spielzeugschaufel-Größe", "Toy shovel size", TechGroup.Tools),
        ["broom"] = ("Besen", "Broom", TechGroup.Tools),
        ["spade"] = ("Spaten", "Spade", TechGroup.Tools),
        ["shovel_size"] = ("Schaufel-Größe", "Shovel size", TechGroup.Tools),
        ["pitchfork"] = ("Heugabel", "Pitchfork", TechGroup.Tools),
        ["fork_size"] = ("Gabel-Größe", "Fork size", TechGroup.Tools),
        ["bucket"] = ("Eimer", "Bucket", TechGroup.Tools),
        ["bucket_size"] = ("Eimer-Größe", "Bucket size", TechGroup.Tools),
        ["wheelbarrow"] = ("Schubkarre", "Wheelbarrow", TechGroup.Tools),
        ["smooth_pour"] = ("Sauberes Ausschütten", "Smooth pour", TechGroup.Tools),
        ["grab_reach"] = ("Greif-Reichweite", "Grab reach", TechGroup.Tools),
        ["easy_swing"] = ("Leichter Schwung", "Easy swing", TechGroup.Tools),

        ["second_wind"] = ("Zweite Puste", "Second wind", TechGroup.Body),
        ["strong_back"] = ("Starker Rücken", "Strong back", TechGroup.Body),
        ["work_boots"] = ("Arbeitsstiefel", "Work boots", TechGroup.Body),
        ["steady_carry"] = ("Ruhiges Tragen", "Steady carry", TechGroup.Body),
        ["jump_height"] = ("Sprunghöhe", "Jump height", TechGroup.Body),
        ["soft_landings"] = ("Weiche Landung", "Soft landings", TechGroup.Body),

        ["cabinet"] = ("Nadel-Vitrine", "Needle cabinet", TechGroup.Machines),
        ["piston_rake"] = ("Kolben-Rechen", "Piston rake", TechGroup.Machines),
        ["rake_wheels"] = ("Rechen-Räder", "Rake wheels", TechGroup.Machines),
        ["rake_bite"] = ("Rechen-Biss", "Rake bite", TechGroup.Machines),
        ["arm_small"] = ("Kleiner Roboterarm", "Small robot arm", TechGroup.Machines),
        ["arm_standard"] = ("Roboterarm", "Robot arm", TechGroup.Machines),
        ["arm_long"] = ("Langer Roboterarm", "Long robot arm", TechGroup.Machines),
        ["arm_speed"] = ("Arm-Tempo", "Arm speed", TechGroup.Machines),
        ["arm_payload"] = ("Arm-Traglast", "Arm payload", TechGroup.Machines),
        ["arm_load"] = ("Arm-Ladung", "Arm load", TechGroup.Machines),
        ["overflow_arm"] = ("Überlauf-Arm", "Overflow arm", TechGroup.Machines),
        ["pick_order"] = ("Greif-Reihenfolge", "Pick order", TechGroup.Machines),
        ["launcher"] = ("Werfer", "Launcher", TechGroup.Machines),
        ["borehole"] = ("Bohrloch", "Borehole", TechGroup.Machines),

        ["electricity"] = ("Elektrizität", "Electricity", TechGroup.Power),
        ["power_pole"] = ("Strommast", "Power pole", TechGroup.Power),
        ["pole_span"] = ("Mast-Spannweite", "Pole span", TechGroup.Power),
        ["pole_drop"] = ("Mast-Abwurf", "Pole drop", TechGroup.Power),
        ["firebox"] = ("Feuerkasten", "Firebox", TechGroup.Power),
        ["generator_output"] = ("Generator-Leistung", "Generator output", TechGroup.Power),

        ["belt"] = ("Förderband", "Conveyor belt", TechGroup.Belts),
        ["belt_speed"] = ("Band-Tempo", "Belt speed", TechGroup.Belts),
        ["belt_links"] = ("Band-Verbindungen", "Belt links", TechGroup.Belts),
        ["enclosed_belt"] = ("Geschlossenes Band", "Enclosed belt", TechGroup.Belts),
        ["splitter"] = ("Verteiler", "Splitter", TechGroup.Belts),
        ["t_splitter"] = ("T-Verteiler", "T-splitter", TechGroup.Belts),
        ["compact_splitter"] = ("Kompakt-Verteiler", "Compact splitter", TechGroup.Belts),
        ["joiner"] = ("Zusammenführer", "Joiner", TechGroup.Belts),
        ["overflow_gate"] = ("Überlauf-Tor", "Overflow gate", TechGroup.Belts),

        ["scanner_mk1"] = ("Scanner Mk1", "Scanner Mk1", TechGroup.Scanning),
        ["scanner_mk2"] = ("Scanner Mk2", "Scanner Mk2", TechGroup.Scanning),
        ["scan_speed"] = ("Scan-Tempo", "Scan speed", TechGroup.Scanning),
        ["scan_batch"] = ("Scan-Menge", "Scan batch", TechGroup.Scanning),
        ["drawer_space"] = ("Schubladen-Platz", "Drawer space", TechGroup.Scanning),

        ["compressor"] = ("Ballenpresse", "Compressor", TechGroup.Bales),
        ["compressor_speed"] = ("Pressen-Tempo", "Compressor speed", TechGroup.Bales),
        ["compressor_batch"] = ("Pressen-Menge", "Compressor batch", TechGroup.Bales),
        ["bale_quality"] = ("Ballen-Qualität", "Bale quality", TechGroup.Bales),
        ["wrapper"] = ("Folienwickler", "Wrapper", TechGroup.Bales),
        ["wrapper_speed"] = ("Wickel-Tempo", "Wrapper speed", TechGroup.Bales),
        ["foil_quality"] = ("Folien-Qualität", "Foil quality", TechGroup.Bales),
        ["pelletizer"] = ("Pelletierer", "Pelletizer", TechGroup.Bales),
        ["pellet_swivel"] = ("Pellet-Drehkopf", "Pellet swivel", TechGroup.Bales),
        ["pellet_speed"] = ("Pellet-Tempo", "Pellet speed", TechGroup.Bales),
        ["pellet_batch"] = ("Pellet-Menge", "Pellet batch", TechGroup.Bales),
        ["hay_price"] = ("Heu-Preis", "Hay price", TechGroup.Bales),

        ["silo"] = ("Silo", "Silo", TechGroup.Storage),
        ["silo_rate"] = ("Silo-Durchsatz", "Silo rate", TechGroup.Storage),
        ["silo_capacity"] = ("Silo-Kapazität", "Silo capacity", TechGroup.Storage),
        ["silo_bulk"] = ("Silo-Schüttmenge", "Silo bulk", TechGroup.Storage),

        ["drone"] = ("Heu-Drohne", "Hay drone", TechGroup.Drones),
        ["drone_collect"] = ("Drohnen-Sammeln", "Drone collect", TechGroup.Drones),
        ["drone_radius"] = ("Drohnen-Radius", "Drone radius", TechGroup.Drones),
        ["drone_speed"] = ("Drohnen-Tempo", "Drone speed", TechGroup.Drones),
    };

    static readonly Dictionary<string, (string De, string En)> Buildings = new()
    {
        ["conveyor"] = ("Förderband", "Conveyor"),
        ["conveyor_splitter"] = ("Verteiler", "Splitter"),
        ["conveyor_u_splitter"] = ("U-Verteiler", "U-splitter"),
        ["conveyor_u_joiner"] = ("U-Zusammenführer", "U-joiner"),
        ["conveyor_joiner"] = ("Zusammenführer", "Joiner"),
        ["conveyor_t_splitter"] = ("T-Verteiler", "T-splitter"),
        ["power_pole"] = ("Strommast", "Power pole"),
        ["hay_generator"] = ("Heu-Generator", "Hay generator"),
        ["robotic_arm"] = ("Roboterarm", "Robot arm"),
        ["haystack_scanner"] = ("Heu-Scanner", "Haystack scanner"),
        ["hay_drone"] = ("Heu-Drohne", "Hay drone"),
        ["hay_compressor"] = ("Ballenpresse", "Compressor"),
        ["hay_wrapper"] = ("Folienwickler", "Wrapper"),
        ["hay_silo"] = ("Silo", "Silo"),
        ["needle_cabinet"] = ("Nadel-Vitrine", "Needle cabinet"),
        ["piston_rake"] = ("Kolben-Rechen", "Piston rake"),
        ["hay_pelletizer"] = ("Pelletierer", "Pelletizer"),
        ["hay_launcher"] = ("Werfer", "Launcher"),
        ["pole"] = ("Strommast", "Power pole"),
        ["stair"] = ("Treppe", "Stairs"),
        ["generator"] = ("Heu-Generator", "Hay generator"),
        ["arm"] = ("Kleiner Roboterarm", "Small robot arm"),
        ["rake"] = ("Rechen", "Rake"),
        ["scanner"] = ("Heu-Scanner", "Haystack scanner"),
        ["u_splitter"] = ("U-Verteiler", "U-splitter"),
        ["u_joiner"] = ("U-Zusammenführer", "U-joiner"),
    };

    /// <summary>Build catalog ids seen in hotbars/build lists.</summary>
    public static readonly string[] BuildIds =
    [
        "belt", "enclosed_belt", "splitter", "t_splitter", "u_splitter", "compact_splitter", "joiner", "u_joiner",
        "pole", "generator", "arm", "arm_standard", "arm_long", "rake", "cabinet", "scanner", "compressor", "wrapper",
        "silo", "drone", "pelletizer", "launcher", "deck", "stair", "haystairs", "haylift", "wall",
    ];

    /// <summary>Hand tools the shop can sell (owned_tools).</summary>
    public static readonly string[] ToolIds = ["sand_shovel", "broom", "spade", "pitchfork", "bucket", "wheelbarrow"];

    public static IEnumerable<string> KnownTechIds => Techs.Keys;

    /// <summary>Which tech an upgrade builds on, derived from the ids (the game's own tree data is not readable).</summary>
    static readonly Dictionary<string, string> Parents = new()
    {
        ["yard_space"] = "yard", ["build_reach"] = "yard", ["wall"] = "yard", ["deck"] = "yard", ["steel_saving"] = "yard",
        ["haystairs"] = "deck", ["haylift"] = "deck",
        ["toy_shovel_size"] = "sand_shovel", ["shovel_size"] = "spade", ["fork_size"] = "pitchfork", ["easy_swing"] = "pitchfork",
        ["bucket_size"] = "bucket", ["smooth_pour"] = "bucket", ["grab_reach"] = "hand_carry",
        ["second_wind"] = "hand_carry", ["strong_back"] = "hand_carry", ["work_boots"] = "hand_carry", ["steady_carry"] = "strong_back",
        ["jump_height"] = "work_boots", ["soft_landings"] = "jump_height",
        ["rake_wheels"] = "piston_rake", ["rake_bite"] = "piston_rake",
        ["arm_speed"] = "arm_small", ["arm_payload"] = "arm_small", ["arm_standard"] = "arm_small",
        ["arm_long"] = "arm_standard", ["arm_load"] = "arm_standard", ["pick_order"] = "arm_standard", ["overflow_arm"] = "arm_standard",
        ["power_pole"] = "electricity", ["pole_span"] = "power_pole", ["pole_drop"] = "power_pole",
        ["firebox"] = "electricity", ["generator_output"] = "electricity",
        ["belt_speed"] = "belt", ["belt_links"] = "belt", ["enclosed_belt"] = "belt", ["splitter"] = "belt", ["joiner"] = "belt",
        ["t_splitter"] = "splitter", ["compact_splitter"] = "splitter", ["overflow_gate"] = "splitter",
        ["scanner_mk2"] = "scanner_mk1", ["scan_speed"] = "scanner_mk1", ["scan_batch"] = "scanner_mk1", ["drawer_space"] = "scanner_mk1",
        ["compressor_speed"] = "compressor", ["compressor_batch"] = "compressor", ["bale_quality"] = "compressor",
        ["wrapper"] = "compressor", ["wrapper_speed"] = "wrapper", ["foil_quality"] = "wrapper",
        ["pellet_swivel"] = "pelletizer", ["pellet_speed"] = "pelletizer", ["pellet_batch"] = "pelletizer",
        ["silo_rate"] = "silo", ["silo_capacity"] = "silo", ["silo_bulk"] = "silo",
        ["drone_collect"] = "drone", ["drone_radius"] = "drone", ["drone_speed"] = "drone",
    };

    public static string? TechParent(string id) => Parents.TryGetValue(id, out var p) ? p : null;

    public static string GroupIcon(TechGroup? g) => g switch
    {
        TechGroup.Tools => "🔧", TechGroup.Body => "💪", TechGroup.Machines => "🦾", TechGroup.Power => "⚡", TechGroup.Belts => "🛤",
        TechGroup.Scanning => "📡", TechGroup.Bales => "🧱", TechGroup.Storage => "🛢", TechGroup.Drones => "🛸", TechGroup.Yard => "🏠",
        _ => "❔",
    };

    public static string TechName(string id) =>
        TechDb.Get(id) is { } card ? card.DisplayName :
        Techs.TryGetValue(id, out var t) ? (Loc.Instance.Language == UiLanguage.German ? t.De : t.En) : Fmt.Pretty(id);

    public static TechGroup? TechGroupOf(string id) => Techs.TryGetValue(id, out var t) ? t.G : null;

    public static string GroupName(TechGroup? g) => g is null ? Loc.T("GroupOther") : Loc.T("Group" + g);

    public static string BuildingName(string id) =>
        Buildings.TryGetValue(id, out var b) ? (Loc.Instance.Language == UiLanguage.German ? b.De : b.En)
        : Techs.ContainsKey(id) ? TechName(id) : Fmt.Pretty(id);

    public static string ItemName(string id) => string.IsNullOrEmpty(id) ? Loc.T("EmptySlot") : BuildingName(id);
}
