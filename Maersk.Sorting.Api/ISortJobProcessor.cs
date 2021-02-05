using System;
using System.Threading.Tasks;

namespace Maersk.Sorting.Api
{
    public interface ISortJobProcessor
    {
        Task<SortJob> Process(SortJob job);
        bool SubmitJob(SortJob job);
        SortJob[] GetAllSortJobs();
        SortJob GetSortJobById(Guid jobId);
    }
}