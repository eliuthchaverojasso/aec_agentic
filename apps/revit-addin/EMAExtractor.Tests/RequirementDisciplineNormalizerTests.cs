using System.Collections.Generic;
using EMAExtractor.Models;
using EMAExtractor.Requirements;
using Xunit;

namespace EMAExtractor.Tests
{
    public class RequirementDisciplineNormalizerTests
    {
        [Theory]
        [InlineData("Electrical", RequirementDiscipline.Electrical)]
        [InlineData("ELECTRICAL", RequirementDiscipline.Electrical)]
        [InlineData("electrical", RequirementDiscipline.Electrical)]
        [InlineData("Elec", RequirementDiscipline.Electrical)]
        [InlineData("Lighting", RequirementDiscipline.Lighting)]
        [InlineData("LIGHTING", RequirementDiscipline.Lighting)]
        [InlineData("Mechanical", RequirementDiscipline.Mechanical)]
        [InlineData("MECH", RequirementDiscipline.Mechanical)]
        [InlineData("HVAC", RequirementDiscipline.Mechanical)]
        [InlineData("Plumbing", RequirementDiscipline.Plumbing)]
        [InlineData("PLUMB", RequirementDiscipline.Plumbing)]
        [InlineData("Technology", RequirementDiscipline.Technology)]
        [InlineData("Low Voltage", RequirementDiscipline.Technology)]
        [InlineData("LV", RequirementDiscipline.Technology)]
        [InlineData("Telecom", RequirementDiscipline.Technology)]
        [InlineData("Data", RequirementDiscipline.Technology)]
        [InlineData("All", RequirementDiscipline.All)]
        public void Parse_MapsCommonSynonyms(string input, RequirementDiscipline expected)
        {
            Assert.Equal(expected, RequirementDisciplineNormalizer.Parse(input));
        }

        [Theory]
        [InlineData(RequirementDiscipline.Electrical, "ELECTRICAL", true)]
        [InlineData(RequirementDiscipline.Electrical, "Elec", true)]
        [InlineData(RequirementDiscipline.Lighting, "LIGHTING", true)]
        [InlineData(RequirementDiscipline.Mechanical, "HVAC", true)]
        [InlineData(RequirementDiscipline.Plumbing, "PLUMB", true)]
        [InlineData(RequirementDiscipline.Technology, "Low Voltage", true)]
        [InlineData(RequirementDiscipline.Technology, "telecom", true)]
        [InlineData(RequirementDiscipline.All, "", true)]
        [InlineData(RequirementDiscipline.Electrical, "", true)]
        [InlineData(RequirementDiscipline.Electrical, "All", true)]
        [InlineData(RequirementDiscipline.Electrical, "Mechanical", false)]
        public void Matches_HandlesSynonymsAndBlankValues(
            RequirementDiscipline selected,
            string rowDiscipline,
            bool expected)
        {
            Assert.Equal(expected, RequirementDisciplineNormalizer.Matches(selected, rowDiscipline));
        }

        [Fact]
        public void RequirementComparisonEngine_DoesNotMarkUppercaseElectricalAsNotApplicable()
        {
            RequirementComparisonEngine engine = new RequirementComparisonEngine();
            List<OwnerRequirementRow> rows = new List<OwnerRequirementRow>
            {
                new OwnerRequirementRow
                {
                    RequirementId = "REQ-1",
                    RequirementText = "General requirement text",
                    Discipline = "ELECTRICAL"
                }
            };

            List<RequirementCheckResult> results = engine.Evaluate(rows, new List<ExportElementRecord>(), RequirementDiscipline.Electrical);

            Assert.Single(results);
            Assert.NotEqual(RequirementCheckStatus.NotApplicable, results[0].Status);
        }
    }
}
