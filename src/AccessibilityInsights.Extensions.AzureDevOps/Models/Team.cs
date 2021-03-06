// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using Newtonsoft.Json;
using System;

namespace AccessibilityInsights.Extensions.AzureDevOps.Models
{
    /// <summary>
    /// Team
    /// </summary>
    public class Team : AzureDevOpsEntity
    {
        public TeamProject ParentProject { get; set; }

        /// <summary>
        /// Default ctor -- used exclusively for JSON serialization
        /// </summary>
        public Team() { }

        public Team(string name, Guid id, TeamProject parent = null) : base(name, id)
        {
            if (parent != null)
            {
                TeamProject typedParent = parent as TeamProject;
                ParentProject = typedParent ?? new TeamProject(parent);
            }
        }

        /// <summary>
        /// Copy ctor - enforces correct types internally
        /// </summary>
        /// <param name="original">The original object being copied</param>
        public Team(Team original) : this(original.Name, original.Id, original.ParentProject) { }
    }
}
