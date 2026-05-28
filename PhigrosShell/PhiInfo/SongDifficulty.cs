namespace PhigrosShell.PhiInfo;

internal class SongDifficulty
{
    public string Name { get; set; } = "";
    public PhigrosArchive.Save.Data.PhiDifficultyInfo<float> Info { get; set; } = default!;

    public float? GetDifficulty(int difficultyIndex) => difficultyIndex switch
    {
        0 => Info.EZ,
        1 => Info.HD,
        2 => Info.IN,
        3 => Info.AT,
        _ => null
    };
}
