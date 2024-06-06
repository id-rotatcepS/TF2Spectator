using NUnit.Framework;
using System.Collections.Generic;
using TF2SpectatorWin;

namespace Tests
{
    public class CommandTests
    {
        [SetUp]
        public void Setup()
        {
        }

        /// <summary>
        /// Random works with 1 entry or more entries
        /// </summary>
        [Test]
        public void CustomCommandFormatRandom()
        {
            CustomCommandFormat iut = new CustomCommandFormat((s, a) => { });
            Assert.That(iut.Format("{random|1}"), Is.EqualTo("1"));
            Assert.That(iut.Format("{random|1|2|3}"), Is.AnyOf("1", "2", "3"));
        }

        /// <summary>
        /// Random is hitting every option
        /// </summary>
        [Test]
        public void CustomCommandFormatRandomDistribution()
        {
            CustomCommandFormat iut = new CustomCommandFormat((s, a) => { });
            Dictionary<string, int> results = new Dictionary<string, int>();
            for (int i = 0; i < 100; i++)
            {
                string result = iut.Format("{random|1|2|3}");
                if (!results.ContainsKey(result)) 
                    results[result] = 0;
                results[result] = results[result] + 1;
            }
            Assert.That(results.Count, Is.EqualTo(3));
            Assert.That(results["1"], Is.GreaterThan(0));
            Assert.That(results["2"], Is.GreaterThan(0));
            Assert.That(results["3"], Is.GreaterThan(0));
        }

        //TODO remaining formatters:
        // deal with {commandname} to replace arg with running command and getting its result (usually a variable output)
        // deal with {commmandname|value:mapped|value2:mapped2} to do both command and mapping together
        // deal with {0|value:mapped|value2:mapped2} to convert arg 0 via map and then format.
        // remaining {0} normal format args

    }
}