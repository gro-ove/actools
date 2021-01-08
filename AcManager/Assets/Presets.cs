using System;
using System.IO.Compression;
using AcManager.Tools.Managers.Presets;
using AcTools.Utils.Helpers;

namespace AcManager.Assets {
    public static class DefaultPresets {
        private static byte[] Unpack(string packed) {
            var bytes = Convert.FromBase64String(packed);
            using (var inputStream = new System.IO.MemoryStream(bytes)) {
                return new DeflateStream(inputStream, CompressionMode.Decompress).ReadAsBytesAndDispose();
            }
        }

        public static void Initialize() {
            PresetsManager.Instance.RegisterBuiltInPreset(
                    Unpack(
                            @"TY7BDoJADET/Zc/EoEdugjEx8QbRc8EiDWWXdLsHYvx3VwX0OM28eX2Y0w2Bz2TRZCoBE7MP6nKmccmlQk1MOhXOqjg22TZNN+lcFOgj2QL7GS07ahVlpeNSqQhDxBao4KBNt/pqb7JdYiqBRsnZVRNvF/IB+AAD3H+WJX6eqCbBK4LM88eAHAd8GMb31l8pZ7A9qv9qny8="),
                    @"Assists", Controls.ControlsStrings.AssistsPreset_Gamer);
            PresetsManager.Instance.RegisterBuiltInPreset(
                    Unpack(
                            @"VY7LCoMwEEX/ZdZSVOgmO7UUCt1F2vVoxxocE0kmCyn999qHQpf3cu6ZecDpRshnYwlUhxwogSKKK9lMoMTHJWvBxrCRuXJWvGNQ+3SX/jiPw/9S96YT8lunF5MWwhFUtq4qjtL2q79oAqg8gdpjK8bZ7UyWwMWEiHzAEe+08mvKP1/Us6crof/pj5F4EYQ4Tm8XqA0qGe1AEr6a5ws="),
                    @"Assists", Controls.ControlsStrings.AssistsPreset_Intermediate);
            PresetsManager.Instance.RegisterBuiltInPreset(
                    Unpack(
                            @"VY7BDoIwEET/pWdi4MpNMCYm3iB4XnCRDUtL2u2BGP/dYgrEY6fz5u1b3Z4IfCeNKu+BHSbq7MUUTPMeVAItMclSGi3WsMrTUxp7FsZ/shqoF7QHHJYqQZhUnm1UyV664cBaFz4TVVvohIzeNSFryHngC0zwCh6xPvS3V5b+7qgXiw8EGwVXjxwWnJ/mdSyma6lg0COKi+LPFw=="),
                    @"Assists", Controls.ControlsStrings.AssistsPreset_Pro);
            PresetsManager.Instance.RegisterBuiltInPreset(
                    Unpack(
                            @"jVTbcpswEP0X+uoyYAO+vPlGkk5cu3EuD5lOR8ZrW7WMqCTiJJ2+9pv6Fe0vdSUBxnHSKU9oz7LaPecs3507ulQbp+d7zWbDOQe63iinF7bDhjPnK7UnAkZ8n0r6DE5vRZiEhtNndJ0OiXB6SuSH8w4EOeeCPvNUEcaeXuZr/BaEoslbaP3r6WolQV0BI4o+nNx9XOuN3HMgS6YHkuOULBgsK2QgyBYuX4UuYaVGnItpBmkVvNKpJ9H5hu9HAm8U/zeM0/Nc758jFBlzBSBGsC6O1zwFzJSU490+FtgtKKRK6zLkjOPtzrs4Dv1wHI2cCr7JauAwGkdxjOCAJNu14Hm6rKH2QdRQcgSM+8MhAhNJyIQvkVvdXnUINAlkyfcTks2NRdBHgWVGcL67QF4dor5kAh4o7KVjh8HsjKY4Xgu75ddPma07zBewI1nR/y1hORQMHCN3G6qgNJ/Vz3Yhy2CMTphQIfhBmUNowHIhtOI290bCgGGvtbNh4EyQpWmyKIDx+JGQ+nmWSFk/z3fHeJ8foZJVK3MFKwaJQkXL0ZR1xGmCvKNqU81X+swyMRBasBR0Gy1N1EHeE+hAQFn7ASzsuVEhff0jv6m/MuHfv1Dq0PXwKTP//CykmRAFgmJXr1SNrNzjx4zLXGh3uFErsMEzstsRndUJbcCoOuM0xS0wV2t25wlWm+OGWCdENmo6KKIm1Qh4hXrlcpIzRTNG9VL6rnUrn2YkoepJ10DLxZTBR2QbvVkY0/2ardGbVoIZl1SZXbt/33I7UTvsdrtR4Ad+JwjbDd8Nmi0dCjuB3213u51G4La9Vsfzmh4mRZ4fRp/LYpecb/s40b0mCynpmqfT8dpdr+1jJKxSY/6AUlm/byDZyhw96XwKv91I74NK2lunWoSDO14s3gkekwTkDEQszMBNfZko3FOYqtrrfvp0ZrxTW4URX5UnfI15kssZI6nV0sQKHQzX2vNJku9y/Sfmae3jF+EB38Lm4OZj8AIdZd7RRYF3Avcz/Guinezchq/xo0Kqzf8cqa4Yvaas/KXawFQsqLLzmqt//AU="),
                    @"Custom Previews", @"Kunos");
            PresetsManager.Instance.RegisterBuiltInPreset(
                    Unpack(
                            @"PY+7CsJAEEV/JUy9LombdydqQLAIprCUFQdc8piwu3mg+O8mYtJMcc4p7ryheNKgierTA1KQ9tZq7BUOBhhkqrKoZ9F0VcVgL2vUMiejrKJmyjeCx2EU+oI5Hve3IkiY4/PIFbEHS34mKnd2il3uMsfl0XyCVWfUQypm9wfHsSXTaYQ08XnA4KCMvFdYDIg2GyG1usOVXuU0sJa6XPgFjXphvvzwo58v"),
                    @"Previews", @"Kunos");
        }
    }
}