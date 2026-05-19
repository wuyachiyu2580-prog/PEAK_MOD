namespace Lantern_ShootZombies_Night
{
    /// <summary>
    /// 灯笼燃料上限选项，在 ModConfig 中以下拉列表显示。
    /// 数值表示燃料秒数，-1 表示无限。
    /// </summary>
    public enum LanternFuelOption
    {
        GameDefault = 0,
        Seconds30 = 30,
        Seconds60 = 60,
        Seconds90 = 90,
        Seconds120 = 120,
        Seconds240 = 240,
        Infinite = -1
    }
}
