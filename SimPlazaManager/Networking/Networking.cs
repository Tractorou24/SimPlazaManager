using Spectre.Console;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System;

namespace SimPlazaManager.Networking;

public class Networking
{
    public static string HttpGet(string url, ProgressTask? progress_task = null) => Task.Run(async () => await HttpGetAsync(url, progress_task)).Result;
    public static async Task<string> HttpGetAsync(string url, ProgressTask? progress_task = null) => Encoding.UTF8.GetString(await HttpGetBytesAsync(url, progress_task));

    public static async Task<byte[]> HttpGetBytesAsync(string url, ProgressTask? progress_task = null)
    {
        if (progress_task is null)
            return await (await _client.GetAsync(url)).Content.ReadAsByteArrayAsync();

        using HttpResponseMessage response = await _client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        progress_task.MaxValue(response.Content.Headers.ContentLength ?? (await response.Content.ReadAsStringAsync()).Length);
        progress_task.StartTask();

        byte[] buffer = new byte[128];
        await using var content_stream = await response.Content.ReadAsStreamAsync();
        using var memory_stream = new MemoryStream((int)(response.Content.Headers.ContentLength ?? 0));

        while (true)
        {
            int read_bytes = await content_stream.ReadAsync(buffer);
            if (read_bytes == 0)
            {
                progress_task.Value = double.MaxValue;
                break;
            }

            await memory_stream.WriteAsync(buffer.AsMemory(0, read_bytes));
            progress_task.Increment(read_bytes);
        }

        progress_task.Value = double.MaxValue;
        return memory_stream.ToArray();
    }

    private static readonly HttpClient _client = new();
}