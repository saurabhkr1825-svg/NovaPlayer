using Nova.Core.Common;
using Nova.Core.Models;

namespace Nova.Core.Interfaces;

public interface IMediaScanner
{
    Task<Result<int>> ScanFolderAsync(string folderPath, bool recursive, IProgress<ScanProgress>? progress, CancellationToken cancellationToken);
}
