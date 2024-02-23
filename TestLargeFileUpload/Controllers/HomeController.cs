 

using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
using System.Configuration;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using Azure.Storage.Blobs.Models;

namespace TestLargeFileUpload.Controllers
{
    /// <summary>
    /// Represents the home controller for handling file upload.
    /// </summary>
    public class HomeController : Controller
    {
        private string connectionString;
        private string blobContainerName;

        private const int chunkSize = 1024 * 1024;

        /// <summary>
        /// Initializes a new instance of the <see cref="HomeController"/> class.
        /// </summary>
        public HomeController()
        {
            connectionString = ConfigurationManager.AppSettings["StorageConnectionString"];
            blobContainerName = ConfigurationManager.AppSettings["BlobContainerName"];
            
        }

        /// <summary>
        /// Displays the index view.
        /// </summary>
        /// <returns>The index view.</returns>
        [HttpGet]
        public ActionResult Index()
        {
            return View();
        }

        /// <summary>
        /// Handles the file upload.
        /// </summary>
        /// <param name="file">The uploaded file.</param>
        /// <returns>The action result.</returns>
        [HttpPost]
        public async Task<ActionResult> Upload(HttpPostedFileBase file)
        {
            try
            {
                if (file == null || file.ContentLength == 0)
                {
                    return RedirectToAction("Error");
                }

                BlobServiceClient blobServiceClient = new BlobServiceClient(connectionString);
                BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(blobContainerName);
                BlockBlobClient blockBlobClient = containerClient.GetBlockBlobClient(file.FileName);

                using (Stream stream = file.InputStream)
                {
                    byte[] buffer = new byte[chunkSize];
                    int bytesRead;
                    int blockNumber = 0;

                    while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        using (MemoryStream chunkStream = new MemoryStream(buffer, 0, bytesRead))
                        {
                            string blockId = Convert.ToBase64String(BitConverter.GetBytes(blockNumber));
                            await blockBlobClient.StageBlockAsync(blockId, chunkStream);

                            blockNumber++;
                        }
                    }
                    var blockList = Enumerable.Range(0, blockNumber)
                        .Select(n => Convert.ToBase64String(BitConverter.GetBytes(n)))
                        .ToList();
                    await blockBlobClient.CommitBlockListAsync(blockList);
                }

                return RedirectToAction("Success");
            }
            catch
            {
                return RedirectToAction("Error");
            }
        }

        /// <summary>
        /// Downloads a file from Azure Blob Storage.
        /// </summary>
        /// <param name="fileName">The name of the file to download.</param>
        /// <returns>The file content as a FileResult.</returns>
        [HttpGet]
        public async Task<FileResult> Download(string fileName)
        {
            try
            {
                // check for a valid filename
                if (string.IsNullOrEmpty(fileName))
                {
                    return null; // or return an appropriate error response
                }


                BlobServiceClient blobServiceClient = new BlobServiceClient(connectionString);
                BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(blobContainerName);
                BlobClient blobClient = containerClient.GetBlobClient(fileName);

                if (!await blobClient.ExistsAsync())
                {
                    return null; // or return an appropriate error response
                }

                BlobDownloadInfo downloadInfo = await blobClient.DownloadAsync();

                MemoryStream memoryStream = new MemoryStream();
                await downloadInfo.Content.CopyToAsync(memoryStream);
                memoryStream.Position = 0;

                return File(memoryStream, downloadInfo.ContentType, fileName);
            }
            catch
            {
                return null; // or return an appropriate error response
            }
        }
            

        /// <summary>
        /// Displays the success view.
        /// </summary>
        /// <returns>The success view.</returns>
        public ActionResult Success()
        {
            return View();
        }

        /// <summary>
        /// Displays the error view.
        /// </summary>
        /// <returns>The error view.</returns>
        public ActionResult Error()
        {
            return View();
        }
    }
}