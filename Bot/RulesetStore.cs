using osu.Game.Rulesets;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Bot
{
    internal class RulesetStore
    {
        private static readonly Dictionary<Assembly, Type> loaded_assemblies = new Dictionary<Assembly, Type>();

        static RulesetStore()
        {
            AppDomain.CurrentDomain.AssemblyResolve += currentDomain_AssemblyResolve;

            // https://github.com/ppy/osu/blob/v2018.201.0/osu.Game/Rulesets/RulesetStore.cs#L20
            foreach (string file in Directory.GetFiles(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), $"{ruleset_library_prefix}.*.dll"))
                loadRulesetFromFile(file);

            AddMissingRulesets();
        }

        /// <summary>
        /// Retrieve a ruleset using a known ID.
        /// </summary>
        /// <param name="id">The ruleset's internal ID.</param>
        /// <returns>A ruleset, if available, else null.</returns>
        public static RulesetInfo GetRuleset(int id) => AvailableRulesets.FirstOrDefault(r => r.ID == id);

        /// <summary>
        /// Retrieve a ruleset using a known short name.
        /// </summary>
        /// <param name="shortName">The ruleset's short name.</param>
        /// <returns>A ruleset, if available, else null.</returns>
        public static RulesetInfo GetRuleset(string shortName) => AvailableRulesets.FirstOrDefault(r => r.ShortName == shortName);

        /// <summary>
        /// All available rulesets.
        /// </summary>
        public static IEnumerable<RulesetInfo> AvailableRulesets;

        private static Assembly currentDomain_AssemblyResolve(object sender, ResolveEventArgs args) => loaded_assemblies.Keys.FirstOrDefault(a => a.FullName == args.Name);

        private const string ruleset_library_prefix = "osu.Game.Rulesets";

        private static void AddMissingRulesets()
        {
            var instances = loaded_assemblies.Values.Select(r => (Ruleset)Activator.CreateInstance(r, (RulesetInfo)null)).ToList();

            var ctxRulesetInfo = new List<RulesetInfo>();

            //add all legacy modes in correct order
            foreach (var r in instances.Where(r => r.LegacyID >= 0).OrderBy(r => r.LegacyID))
            {
                if (ctxRulesetInfo.SingleOrDefault(rsi => rsi.ID == r.RulesetInfo.ID) == null)
                    ctxRulesetInfo.Add(r.RulesetInfo);
            }

            //add any other modes
            foreach (var r in instances.Where(r => r.LegacyID < 0))
                if (ctxRulesetInfo.FirstOrDefault(ri => ri.InstantiationInfo == r.RulesetInfo.InstantiationInfo) == null)
                    ctxRulesetInfo.Add(r.RulesetInfo);

            //perform a consistency check
            foreach (var r in ctxRulesetInfo)
            {
                try
                {
                    var instance = r.CreateInstance();

                    r.Name = instance.Description;
                    r.ShortName = instance.ShortName;

                    r.Available = true;
                }
                catch
                {
                    r.Available = false;
                }
            }

            AvailableRulesets = ctxRulesetInfo.Where(r => r.Available).ToList();
        }

        private static void loadRulesetFromFile(string file)
        {
            var filename = Path.GetFileNameWithoutExtension(file);

            if (loaded_assemblies.Values.Any(t => t.Namespace == filename))
                return;

            try
            {
                var assembly = Assembly.LoadFrom(file);
                loaded_assemblies[assembly] = assembly.GetTypes().First(t => t.IsSubclassOf(typeof(Ruleset)));
            }
            catch (Exception) { }
        }
    }
}