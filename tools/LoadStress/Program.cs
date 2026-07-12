using Bdtm.Core;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Processing;

string folder = args.ElementAtOrDefault(0)
    ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "Projects/toolbox/datasets/images/功能测试集");

Console.WriteLine("folder=" + folder);
var dm = new DatasetManager();
var sw = System.Diagnostics.Stopwatch.StartNew();
bool ok = dm.LoadFromFolder(folder, new DatasetLoadOptions { FixTagsOnSaveLoad = true });
sw.Stop();
Console.WriteLine($"load ok={ok} count={dm.DataSet.Count} ms={sw.ElapsedMilliseconds}");

int n = 0, fail = 0;
foreach (var item in dm.GetDataSource().Take(50))
{
    string path = item.ImageFilePath;
    string ext = Path.GetExtension(path).ToLowerInvariant();
    if (ext is not (".png" or ".jpg" or ".jpeg" or ".webp" or ".bmp"))
        continue;
    try
    {
        using var image = Image.Load(path);
        image.Mutate(c => c.Resize(new ResizeOptions { Size = new Size(96, 96), Mode = ResizeMode.Max }));
        using var ms = new MemoryStream();
        image.Save(ms, new PngEncoder());
        n++;
    }
    catch (Exception ex)
    {
        fail++;
        Console.WriteLine("thumb fail " + path + " : " + ex.Message);
    }
}
Console.WriteLine($"thumbs ok={n} fail={fail}");
Console.WriteLine("DONE");
