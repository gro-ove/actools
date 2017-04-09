namespace AcTools.AiFile {
    public struct AiPoint {
        public int Id;

        // 3D-vector
        public float[] Position;
        public float Length;

        // 18 different values
        public float[] Extra;

        public float Grade => Extra[17];

        public float Camber => Extra[9];

        public float Radius => Extra[4];

        public float SideLeft => Extra[5];

        public float SideRight => Extra[6];

        public float Width => SideLeft + SideRight;

        public static float[] CreateExtra(float sideLeft, float sideRight) {
            var result = new float[18];
            result[5] = sideLeft;
            result[6] = sideRight;
            return result;
        }
    }
}