namespace SistemaDeTienda.Services.IServices;

public interface IStorageService
{
    /// <summary>
    /// Sube un archivo al almacenamiento en la nube.
    /// </summary>
    /// <param name="fileStream">Flujo de datos del archivo.</param>
    /// <param name="fileName">Nombre sugerido para el archivo (se generará un UUID).</param>
    /// <param name="contentType">Tipo MIME del archivo.</param>
    /// <returns>La URL pública del archivo subido.</returns>
    Task<string> UploadFileAsync(Stream fileStream, string fileName, string contentType);

    /// <summary>
    /// Elimina un archivo del almacenamiento en la nube.
    /// </summary>
    /// <param name="fileUrl">URL completa del archivo a eliminar.</param>
    /// <returns>Verdadero si la operación fue exitosa.</returns>
    Task<bool> DeleteFileAsync(string fileUrl);
}
