// Glyphs.cs — Glifos compartidos de Segoe Fluent Icons / Segoe MDL2 Assets.
// Definidos por código numérico para mantener los fuentes en ASCII puro.
using AudioLeap.Core.Audio;

namespace AudioLeap.UI.Common;

public static class Glyphs
{
    public static readonly string Mute = char.ConvertFromUtf32(0xE74F);
    public static readonly string Speakers = char.ConvertFromUtf32(0xE7F5);
    public static readonly string Headphones = char.ConvertFromUtf32(0xE7F6);
    public static readonly string Monitor = char.ConvertFromUtf32(0xE7F4);
    public static readonly string Volume = char.ConvertFromUtf32(0xE767);
    public static readonly string Check = char.ConvertFromUtf32(0xE73E);

    public static string For(AudioFormFactor form) => form switch
    {
        AudioFormFactor.Headphones or AudioFormFactor.Headset => Headphones,
        AudioFormFactor.DigitalAudioDisplayDevice => Monitor,
        AudioFormFactor.SPDIF or AudioFormFactor.UnknownDigitalPassthrough => Volume,
        _ => Speakers,
    };
}
