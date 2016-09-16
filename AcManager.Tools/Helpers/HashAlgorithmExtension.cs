namespace AcManager.Tools.Helpers {
    public static class HashAlgorithmExtension {
        public static string ToHexString(this byte[] data) {
            var lookup = new[] { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'A', 'B', 'C', 'D', 'E', 'F' };
            int i = -1, p = -1, l = data.Length;
            var c = new char[l-- * 2];
            while (i < l) {
                var d = data[++i];
                c[++p] = lookup[d >> 4];
                c[++p] = lookup[d & 0xF];
            }
            return new string(c, 0, c.Length);
        }
    }
}
