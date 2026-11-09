namespace CheryTools.Overlayer.Preview
{
    internal static class OvNodeRuntime
    {
        internal static void Preview(OvTextComponent text) => OvRuntime.Host?.Preview(text);
        internal static void Preview(OvImageComponent image) => OvRuntime.Host?.Preview(image);
        internal static void Stop(OvTextComponent text) => OvRuntime.Host?.StopPreview(text);
        internal static void Stop(OvImageComponent image) => OvRuntime.Host?.StopPreview(image);
    }
}
