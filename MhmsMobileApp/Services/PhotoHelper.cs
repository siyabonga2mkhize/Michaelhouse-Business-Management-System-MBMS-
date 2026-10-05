using Microsoft.Maui.Media;
using System;
using System.IO;
using System.Threading.Tasks;
#if ANDROID
using Android.Graphics;
using Android.Media;
#endif

namespace MhmsMobileApp.Services
{
    // ============================================================
    // Takes a photo with the device camera and prepares it for the
    // server: upright and at most maxSize pixels on the long side
    // (JPEG) — 800 is plenty for a face; invoices need more so the
    // text can be read. The image work runs on a background thread so the
    // screen never freezes.
    // ============================================================
    public static class PhotoHelper
    {
        public const int FaceSize = 800;
        public const int DocumentSize = 2000;

        public static bool CanTakePhoto => MediaPicker.Default.IsCaptureSupported;

        // Null when the person closed the camera without taking a photo
        public static async Task<byte[]?> TakePhotoAsync(int maxSize = FaceSize)
        {
            var file = await MediaPicker.Default.CapturePhotoAsync();
            if (file == null) return null;

            return await Task.Run(async () =>
            {
                byte[] original;
                using (var stream = await file.OpenReadAsync())
                using (var buffer = new MemoryStream())
                {
                    await stream.CopyToAsync(buffer);
                    original = buffer.ToArray();
                }

                return Prepare(original, file.FullPath, maxSize);
            });
        }

#if ANDROID
        // Android: shrink, turn upright (cameras store the rotation in the
        // photo's EXIF data rather than rotating the pixels), save as JPEG
        private static byte[] Prepare(byte[] original, string path, int maxSize)
        {
            try
            {
                var bounds = new BitmapFactory.Options { InJustDecodeBounds = true };
                BitmapFactory.DecodeByteArray(original, 0, original.Length, bounds);

                int sample = 1;
                while (Math.Max(bounds.OutWidth, bounds.OutHeight) / (sample * 2) >= maxSize) sample *= 2;

                using var bitmap = BitmapFactory.DecodeByteArray(original, 0, original.Length,
                    new BitmapFactory.Options { InSampleSize = sample });
                if (bitmap == null) return original;

                float scale = Math.Min(1f, maxSize / (float)Math.Max(bitmap.Width, bitmap.Height));
                var matrix = new Matrix();
                matrix.PostScale(scale, scale);

                int rotation = ExifRotation(path);
                if (rotation != 0) matrix.PostRotate(rotation);

                using var upright = Bitmap.CreateBitmap(bitmap, 0, 0, bitmap.Width, bitmap.Height, matrix, true);
                using var output = new MemoryStream();
                upright!.Compress(Bitmap.CompressFormat.Jpeg!, 85, output);
                return output.ToArray();
            }
            catch
            {
                return original;   // the server can still read the original
            }
        }

        private static int ExifRotation(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) return 0;

                var exif = new ExifInterface(path);
                switch (exif.GetAttributeInt(ExifInterface.TagOrientation, 1))
                {
                    case 6: return 90;
                    case 3: return 180;
                    case 8: return 270;
                    default: return 0;
                }
            }
            catch
            {
                return 0;
            }
        }
#else
        // Windows: photos are already upright; send as taken
        private static byte[] Prepare(byte[] original, string path, int maxSize) => original;
#endif
    }
}
