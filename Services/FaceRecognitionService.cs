using System;
using System.Linq;
using System.Threading.Tasks;
using FaceAiSharp;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Image = SixLabors.ImageSharp.Image;

namespace VolcanoMonitor.Services
{
    public class FaceRecognitionService
    {
        private readonly IFaceDetectorWithLandmarks _detector;
        private readonly IFaceEmbeddingsGenerator _recognizer;

        public FaceRecognitionService()
        {
            _detector   = FaceAiSharpBundleFactory.CreateFaceDetectorWithLandmarks();
            _recognizer = FaceAiSharpBundleFactory.CreateFaceEmbeddingsGenerator();
        }

        // Sinkron: deteksi wajah + buat embedding dari byte[] foto.
        // Return null jika tidak ada wajah terdeteksi.
        public float[]? DetectAndEmbed(byte[] imageBytes)
        {
            using var img = Image.Load<Rgb24>(imageBytes);
            var faces = _detector.DetectFaces(img);
            if (faces.Count == 0) return null;
            var face = faces.OrderByDescending(f => f.Confidence ?? 0f).First();
            if (face.Landmarks is null) return null;
            _recognizer.AlignFaceUsingLandmarks(img, face.Landmarks);
            return _recognizer.GenerateEmbedding(img);
        }

        // Async: jalankan di background thread agar UI tidak freeze
        public Task<float[]?> DetectAndEmbedAsync(byte[] imageBytes)
            => Task.Run(() => DetectAndEmbed(imageBytes));

        // Cosine similarity -> persentase 0..100%
        public double CompareSimilarity(float[] a, float[] b)
        {
            if (a == null || b == null || a.Length != b.Length) return 0;
            double dot = 0, na = 0, nb = 0;
            for (int i = 0; i < a.Length; i++)
            { dot += a[i] * b[i]; na += a[i] * a[i]; nb += b[i] * b[i]; }
            if (na == 0 || nb == 0) return 0;
            double cos = dot / (Math.Sqrt(na) * Math.Sqrt(nb));
            return Math.Clamp(cos, 0, 1) * 100.0;
        }

        public byte[] SerializeEmbedding(float[] e)
        {
            var b = new byte[e.Length * sizeof(float)];
            Buffer.BlockCopy(e, 0, b, 0, b.Length);
            return b;
        }

        public float[] DeserializeEmbedding(byte[] b)
        {
            var e = new float[b.Length / sizeof(float)];
            Buffer.BlockCopy(b, 0, e, 0, b.Length);
            return e;
        }
    }
}