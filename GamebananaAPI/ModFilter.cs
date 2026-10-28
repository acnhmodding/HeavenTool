namespace GamebananaAPI;

/// <summary>What to narrow a Mod/Index listing to.</summary>
public class ModFilter
{
    /// <summary>Text the mod's name must contain.</summary>
    public string? Name { get; set; }

    /// <summary>A root category or a subcategory.</summary>
    public int? CategoryId { get; set; }

    /// <summary>The member who submitted the mod.</summary>
    public long? SubmitterId { get; set; }

    /// <summary>Only mods GameBanana has featured.</summary>
    public bool FeaturedOnly { get; set; }

    public ModSort Sort { get; set; }
}
