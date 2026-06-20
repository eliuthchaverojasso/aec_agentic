using System;
using System.Collections.Generic;
using System.Linq;
using EMAExtractor.Enums;
using EMAExtractor.Models;

namespace EMAExtractor.Services
{
    public static class ExportJobService
    {
        private static readonly object Gate = new object();
        private static readonly List<ExportJob> Jobs = new List<ExportJob>();

        public static ExportJob StartJob(ExportDiscipline discipline, string outputPath)
        {
            ExportJob job = new ExportJob
            {
                ExportType = discipline.ToString().ToLowerInvariant(),
                Discipline = discipline.ToString().ToUpperInvariant(),
                Status = "running",
                Phase = "collecting_elements",
                OutputPath = outputPath,
                StartedAt = DateTime.Now
            };
            lock (Gate)
            {
                Jobs.Insert(0, job);
            }
            LoggingService.Info($"Export job started: {job.JobId} {job.Discipline}");
            return job;
        }

        public static void Update(ExportJob job, string phase, int current, int total)
        {
            if (job == null)
            {
                return;
            }

            job.Phase = phase;
            job.ElementCount = current;
            job.ProgressPercent = total <= 0 ? 0 : Math.Min(100, (int)Math.Round(current * 100.0 / total));
        }

        public static void Complete(ExportJob job, int elementCount)
        {
            if (job == null)
            {
                return;
            }

            job.Status = "completed";
            job.Phase = "done";
            job.ProgressPercent = 100;
            job.ElementCount = elementCount;
            job.CompletedAt = DateTime.Now;
            LoggingService.Info($"Export job completed: {job.JobId}, elements={elementCount}, output={job.OutputPath}");
        }

        public static void Fail(ExportJob job, Exception exception)
        {
            if (job == null)
            {
                return;
            }

            job.Status = "failed";
            job.Phase = "failed";
            job.ErrorMessage = exception.Message;
            job.CompletedAt = DateTime.Now;
            LoggingService.Error($"Export job failed: {job.JobId}", exception);
        }

        public static IReadOnlyList<ExportJob> GetJobs()
        {
            lock (Gate)
            {
                return Jobs.ToList();
            }
        }
    }
}
