using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Maersk.Sorting.Api
{
    public class SortJobProcessor : ISortJobProcessor
    {
        private readonly ILogger<SortJobProcessor> _logger;
        private readonly ConcurrentQueue<SortJob> _jobQueue = new ConcurrentQueue<SortJob>();
        private readonly IMemoryCache _memoryCache;

        public SortJobProcessor(ILogger<SortJobProcessor> logger, IMemoryCache memoryCache)
        {
            _logger = logger;
            _memoryCache = memoryCache;
            Task.Run(() => BackgroundProcess());
        }

        /// <summary>
        /// This method is to push the job into the concurrent queue.
        /// </summary>
        /// <param name="job"></param>
        /// <returns></returns>
        public bool SubmitJob(SortJob job)
        {
            bool IsSubmitted = false;
            try
            {
                _logger.LogInformation("Pushing job into concurrent queue with ID '{JobId}'.", job.Id);

                if (!_jobQueue.Any(x => x.Id == job.Id))
                {
                    _memoryCache.Set(job.Id, job);
                    _jobQueue.Enqueue(job);
                    IsSubmitted = true;
                }
            }
            catch (Exception)
            {
                throw;
            }
            return IsSubmitted;
        }

        /// <summary>
        /// This method is to perform background process.
        /// </summary>
        private async void BackgroundProcess()
        {
            _logger.LogInformation("Processing background jobs");

            while (true)
            {
                if (_jobQueue.Count <= 0)
                {
                    await Task.Delay(10000);
                }
                else
                {
                    SortJob? jobInQueue;
                    if (!_jobQueue.TryPeek(out jobInQueue))
                    {
                        _logger.LogInformation("Error in peeking job in queue.");
                    }
                    else
                    {
                        var response = await Process(jobInQueue);
                        if (_jobQueue.TryDequeue(out _))
                        {
                            _logger.LogInformation("Removing the job in memory cache to update the status.");

                            _memoryCache.Remove(response.Id);

                            _logger.LogInformation("Adding the job in memory cache with updated status.");

                            _memoryCache.Set(response.Id, response);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// This method is to proccess the sort job queue.
        /// </summary>
        /// <param name="job"></param>
        /// <returns></returns>
        public async Task<SortJob> Process(SortJob job)
        {
            _logger.LogInformation("Processing job with ID '{JobId}'.", job.Id);

            var stopwatch = Stopwatch.StartNew();

            var output = job.Input.OrderBy(n => n).ToArray();
            await Task.Delay(1); // NOTE: This is just to simulate a more expensive operation

            var duration = stopwatch.Elapsed;

            _logger.LogInformation("Completed processing job with ID '{JobId}'. Duration: '{Duration}'.", job.Id, duration);


            return new SortJob(
                id: job.Id,
                status: SortJobStatus.Completed,
                duration: duration,
                input: job.Input,
                output: output);
        }

        /// <summary>
        /// This method is to get all sort jobs.
        /// </summary>
        /// <returns></returns>
        public SortJob[] GetAllSortJobs()
        {
            try
            {
                var cacheEntriesCollectionDefinition = typeof(MemoryCache).GetProperty("EntriesCollection", BindingFlags.NonPublic | BindingFlags.Instance);
                var cacheEntriesCollection = cacheEntriesCollectionDefinition?.GetValue(_memoryCache) as dynamic;
                List<ICacheEntry> cacheCollectionValues = new List<ICacheEntry>();

                if (cacheEntriesCollection != null)
                {
                    foreach (var cacheItem in cacheEntriesCollection)
                    {
                        if (cacheItem != null)
                        {
                            ICacheEntry cacheItemValue = cacheItem.GetType().GetProperty("Value").GetValue(cacheItem, null);
                            cacheCollectionValues.Add(cacheItemValue);
                        }
                    }
                }

                return cacheCollectionValues != null ? cacheCollectionValues.Select(s => (SortJob)s.Value).ToArray() : new SortJob[] { };
            }
            catch (Exception)
            {
                throw;
            }           
        }

        /// <summary>
        /// This method is to get sort job by id.
        /// </summary>
        /// <param name="jobId"></param>
        /// <returns></returns>
        public SortJob GetSortJobById(Guid jobId)
        {
            try
            {
                return _memoryCache.Get<SortJob>(jobId);
            }
            catch (Exception)
            {
                throw;
            }
        }
    }
}
