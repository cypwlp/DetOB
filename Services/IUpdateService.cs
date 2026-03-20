using OB.Models;
using Prism.Dialogs;
using System.Threading.Tasks;

namespace OB.Services
{
    public interface IUpdateService
    {
        Task<(bool IsChina, UpdateChannel Channel)> DetectUpdateChannelAsync();
        Task CheckAndUpdateAsync(IDialogService dialogService);
        Task CheckGitHubVelopackUpdateAsync(IDialogService dialogService);
        Task CheckInternalUpdateAsync(IDialogService dialogService);
    }
}