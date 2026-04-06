using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Transfer;
using SistemaDeTienda.Services.IServices;
using Microsoft.Extensions.Configuration;

namespace SistemaDeTienda.Services;

public class R2StorageService : IStorageService
{
    private readonly IAmazonS3 _s3Client;
    private readonly string _bucketName;
    private readonly string _publicUrl;

    public R2StorageService(IConfiguration configuration)
    {
        var config = configuration.GetSection("CloudflareR2");
        _bucketName = config["BucketName"] ?? throw new ArgumentNullException("BucketName is not configured");
        _publicUrl = config["PublicUrl"] ?? throw new ArgumentNullException("PublicUrl is not configured");

        var s3Config = new AmazonS3Config
        {
            ServiceURL = config["ServiceUrl"],
            ForcePathStyle = true // Requerido para R2 en algunos casos
        };

        _s3Client = new AmazonS3Client(config["AccessKey"], config["SecretKey"], s3Config);
    }

    public async Task<string> UploadFileAsync(Stream fileStream, string fileName, string contentType)
    {
        // Generar un nombre único para evitar colisiones
        var extension = Path.GetExtension(fileName);
        var uniqueFileName = $"{Guid.NewGuid()}{extension}";
        
        // Carpeta virtual para organizar
        var key = $"productos/{uniqueFileName}";

        var request = new PutObjectRequest
        {
            BucketName = _bucketName,
            Key = key,
            InputStream = fileStream,
            ContentType = contentType,
            // R2 no usa ACLs tradicionales de S3 para acceso público, 
            // se configura a nivel de bucket/dominio en Cloudflare.
        };

        await _s3Client.PutObjectAsync(request);

        // Retornar la URL pública (usando el PublicDevelopmentURL o Custom Domain configurado)
        return $"{_publicUrl.TrimEnd('/')}/{key}";
    }

    public async Task<bool> DeleteFileAsync(string fileUrl)
    {
        if (string.IsNullOrWhiteSpace(fileUrl)) return false;

        try
        {
            // Extraer el Key de la URL pública
            // Ejemplo: https://pub-xxx.r2.dev/productos/uuid.jpg -> productos/uuid.jpg
            var uri = new Uri(fileUrl);
            var key = uri.AbsolutePath.TrimStart('/');

            var request = new DeleteObjectRequest
            {
                BucketName = _bucketName,
                Key = key
            };

            var response = await _s3Client.DeleteObjectAsync(request);
            return response.HttpStatusCode == System.Net.HttpStatusCode.NoContent || 
                   response.HttpStatusCode == System.Net.HttpStatusCode.OK;
        }
        catch (Exception)
        {
            // Loguear error si fuera necesario
            return false;
        }
    }
}
