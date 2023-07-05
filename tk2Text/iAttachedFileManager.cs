using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ImageMagick;
using Nekote;

namespace tk2Text
{
    internal class iAttachedFileManager
    {
        public readonly string DestRelativeFilePath;

        public readonly string DestFilePath;

        public readonly iAttachedFileInfo File;

        public readonly bool IsImage;

        public readonly bool IsOptimized;

        public readonly string? OptimizedImageRelativeFilePath;

        public readonly string? OptimizedImageFilePath;

        // 拡張子に関わらず一度開いてみるとか、それで画像なら必要に応じて拡張子を修正するとかも可能だが、
        //     それをするなら受け子の taskKiller の方であり、こちらでやると実装が複雑になる

        public static readonly IEnumerable <string> ImageFileExtensions = new [] { ".bmp", ".gif", ".jpeg", ".jpg", ".png", ".tif", ".tiff" };

        public static readonly IEnumerable <string> ImageFileExtensionsToOptimize = new [] { ".bmp", ".jpeg", ".jpg", ".tif", ".tiff" };

        public iAttachedFileManager (string destRelativeFilePath, string destFilePath, iAttachedFileInfo file)
        {
            DestRelativeFilePath = destRelativeFilePath;
            DestFilePath = destFilePath;
            File = file;

            // 画像としての正当性を評価せずにパスを確定する甘い実装
            // taskKiller にファイルを添付するのも自分なので、これで様子見

            IsImage = ImageFileExtensions.Contains (file.File.Extension, StringComparer.OrdinalIgnoreCase);
            IsOptimized = ImageFileExtensionsToOptimize.Contains (file.File.Extension, StringComparer.OrdinalIgnoreCase);

            if (IsOptimized)
            {
                static string iGetOptimizedImagePath (string path)
                {
                    // Attached/Optimized/Hoge.jpg なども考えたが、ファイル名の一意性を保つことを優先した
                    // -Optimized 方式では元々 -Optimized が入っているファイルとの衝突のリスクがあるが、
                    //     A.jpg として入れたものが A.jpg として下りてきて内容が変わっていて既存の A.jpg と一致しない不都合の方が大きい
                    return nPath.Combine (nPath.GetDirectoryPath (path), nPath.GetNameWithoutExtension (path) + "-Optimized" + nPath.GetExtension (path));
                }

                OptimizedImageRelativeFilePath = iGetOptimizedImagePath (DestRelativeFilePath);
                OptimizedImageFilePath = iGetOptimizedImagePath (DestFilePath);
            }
        }

        public void Resize ()
        {
            using (MagickImage xImage = new MagickImage (DestFilePath))
            {
                // photoPage のパラメーターを拝借
                // 縮小版からバッキバキの画質でないといけないシステムでない

                xImage.Format = MagickFormat.Jpeg;
                xImage.Quality = 75;
                xImage.AutoOrient ();
                xImage.Resize (1280, 1280);
                xImage.Strip ();
                xImage.Write (OptimizedImageFilePath!); // 上書きモード
            }
        }
    }
}
