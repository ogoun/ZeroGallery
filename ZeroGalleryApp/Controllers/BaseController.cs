using Microsoft.AspNetCore.Mvc;
using ZeroGallery.Shared;
using ZeroGallery.Shared.Models;
using ZeroLevel;

namespace ZeroGalleryApp.Controllers
{
    public abstract class BaseController
        : Controller
    {
        private readonly string _uploadToken;
        private readonly string _masterToken;
        private readonly bool _existMasterToken;
        private readonly bool _existUploadToken;

        public BaseController(AppConfig config)
        {
            _masterToken = config.api_master_token ?? string.Empty;
            _uploadToken = config.api_write_token ?? string.Empty;
            _existMasterToken = !string.IsNullOrWhiteSpace(_masterToken);
            _existUploadToken = !string.IsNullOrWhiteSpace(_uploadToken);
        }

        /// <summary>
        /// Токены не заданы, всем разрешено всё
        /// </summary>
        /// <returns></returns>
        private bool IsFullAccess() => _existMasterToken == false && _existUploadToken == false;
        public bool HasAdminAccess() => _existMasterToken && OperationContext.UploadToken!.IsEqual(_masterToken);
        private bool CanUpload() => _existUploadToken && OperationContext.UploadToken!.IsEqual(_uploadToken);
        private bool HasAlbumAccess(string token) => _existUploadToken && OperationContext.AccessToken!.IsEqual(token);

        protected void Error(Exception ex, string message)
        {
            var context = OperationContext;
            Log.Error(ex, $"{message}. Upload token: '{context.UploadToken}'. Access token: '{context.AccessToken}'");
        }

        protected bool CanCreateAlbum()
        {
            if (IsFullAccess() || HasAdminAccess() || CanUpload()) return true;
            return false;
        }

        protected bool CanRemoveAlbum(string albumToken)
        {
            if (IsFullAccess() || HasAdminAccess()) return true;
            if (CanUpload() && (string.IsNullOrWhiteSpace(albumToken) || HasAlbumAccess(albumToken))) return true;
            return false;
        }

        protected bool CanRemoveItem(string albumToken)
        {
            if (IsFullAccess() || HasAdminAccess()) return true;
            if (CanUpload() && (string.IsNullOrWhiteSpace(albumToken) || HasAlbumAccess(albumToken))) return true;
            return false;
        }

        protected bool  CanUploadImages(string albumToken)
        {
            if (IsFullAccess() || HasAdminAccess()) return true;
            if (CanUpload() && (string.IsNullOrWhiteSpace(albumToken) || HasAlbumAccess(albumToken))) return true;
            return false;
        }

        protected bool CanViewImages(string albumToken)
        {
            if (IsFullAccess() || HasAdminAccess()) return true;
            if (string.IsNullOrWhiteSpace(albumToken) || HasAlbumAccess(albumToken)) return true;
            return false;
        }

        protected OperationContext OperationContext
        {
            get
            {
                return (HttpContext.Items["op_context"] as OperationContext)!;
            }
        }
    }
}
