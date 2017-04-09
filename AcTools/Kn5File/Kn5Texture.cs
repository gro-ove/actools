namespace AcTools.Kn5File {
    public class Kn5Texture {
        public string Name;
        public bool Active;
        public int Length;

        public Kn5Texture Clone() {
            return new Kn5Texture {
                Name = Name,
                Active = Active,
                Length = Length
            };
        }
    }
}
