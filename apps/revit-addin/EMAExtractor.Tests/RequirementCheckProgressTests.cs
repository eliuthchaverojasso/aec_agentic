using System;
using System.Collections.Generic;
using System.Linq;
using EMAExtractor.Models;
using Xunit;

namespace EMAExtractor.Tests
{
    public class RequirementCheckProgressTests
    {
        [Fact]
        public void Clamp_OverallPercent_ClampsToRange()
        {
            RequirementCheckProgress p = new RequirementCheckProgress
            {
                OverallPercent = 150,
                StagePercent = -20
            };

            p.Clamp();

            Assert.Equal(100, p.OverallPercent);
            Assert.Equal(0, p.StagePercent);
        }

        [Fact]
        public void Clamp_ValidValues_Unchanged()
        {
            RequirementCheckProgress p = new RequirementCheckProgress
            {
                OverallPercent = 55.5,
                StagePercent = 80.0
            };

            p.Clamp();

            Assert.Equal(55.5, p.OverallPercent);
            Assert.Equal(80.0, p.StagePercent);
        }

        [Fact]
        public void BuildDefaultStages_Returns10Stages()
        {
            List<StageInfo> stages = RequirementCheckProgress.BuildDefaultStages();

            Assert.Equal(10, stages.Count);
            Assert.Equal("Preparing requirements", stages[0].Name);
            Assert.Equal("Complete", stages[9].Name);
        }

        [Fact]
        public void BuildDefaultStages_AllStartWaiting()
        {
            List<StageInfo> stages = RequirementCheckProgress.BuildDefaultStages();

            Assert.All(stages, s => Assert.Equal(StageStatus.Waiting, s.Status));
        }

        [Fact]
        public void BuildDefaultStages_IndicesAreSequential()
        {
            List<StageInfo> stages = RequirementCheckProgress.BuildDefaultStages();

            for (int i = 0; i < stages.Count; i++)
            {
                Assert.Equal(i + 1, stages[i].Index);
            }
        }

        [Fact]
        public void StageInfo_StatusTransitions()
        {
            StageInfo stage = new StageInfo(1, "Test stage");

            Assert.Equal(StageStatus.Waiting, stage.Status);

            stage.Status = StageStatus.Running;
            Assert.Equal(StageStatus.Running, stage.Status);

            stage.Status = StageStatus.Complete;
            stage.ElapsedMs = 1234;
            Assert.Equal(StageStatus.Complete, stage.Status);
            Assert.Equal(1234, stage.ElapsedMs);
        }

        [Fact]
        public void Progress_DefaultDetailLinesNotNull()
        {
            RequirementCheckProgress p = new RequirementCheckProgress();

            Assert.NotNull(p.DetailLines);
            Assert.Empty(p.DetailLines);
        }

        [Fact]
        public void Progress_DefaultStagesNotNull()
        {
            RequirementCheckProgress p = new RequirementCheckProgress();

            Assert.NotNull(p.Stages);
            Assert.Empty(p.Stages);
        }

        [Fact]
        public void Clamp_BoundaryValues()
        {
            RequirementCheckProgress p1 = new RequirementCheckProgress { OverallPercent = 0, StagePercent = 100 };
            p1.Clamp();
            Assert.Equal(0, p1.OverallPercent);
            Assert.Equal(100, p1.StagePercent);

            RequirementCheckProgress p2 = new RequirementCheckProgress { OverallPercent = 100, StagePercent = 0 };
            p2.Clamp();
            Assert.Equal(100, p2.OverallPercent);
            Assert.Equal(0, p2.StagePercent);
        }
    }
}
