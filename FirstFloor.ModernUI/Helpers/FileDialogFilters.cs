using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FirstFloor.ModernUI.Helpers {
    public static class FileDialogFilters {
        public const string ImagesFilter = "Image Files|*.jpg;*.jpeg;*.png;*.gif;*.tif|All files (*.*)|*.*";
        public const string TextureFilter = "DDS & TIFF Files|*.dds;*.tif;*.tiff|Image Files|*.dds;*.tif;*.tiff;*.jpg;*.jpeg;*.png|All files (*.*)|*.*";
    }
}
