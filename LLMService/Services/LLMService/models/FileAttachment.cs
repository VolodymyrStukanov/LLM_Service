namespace LLMService.Services.LLMService.models
{
    public record FileAttachment
    {
        public required string FileName { get; set; }
        public required string ContentType { get; set; }
        public required byte[] Content { get; set; }
        public long Size { get; set; }

        public string Extension => Path.GetExtension(FileName).ToLowerInvariant();
        public string GetBase64Content() => Convert.ToBase64String(Content);

        public static async Task<FileAttachment> FromFormFileAsync(IFormFile formFile)
        {
            using var ms = new MemoryStream();
            await formFile.CopyToAsync(ms);
            
            return new FileAttachment
            {
                FileName = formFile.FileName,
                ContentType = formFile.ContentType,
                Content = ms.ToArray(),
                Size = formFile.Length
            };
        }
    }
}