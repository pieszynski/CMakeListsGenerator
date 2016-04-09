//------------------------------------------------------------------------------
// <copyright file="CMakeListsTxtCommand.cs" company="Company">
//     Copyright (c) Company.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

using System;
using System.ComponentModel.Design;
using System.Globalization;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio;
using System.Runtime.InteropServices;
using EnvDTE;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace Pieszynski.CMakeListsGenerator
{
    /// <summary>
    /// Command handler
    /// </summary>
    internal sealed class CMakeListsTxtCommand
    {
        /// <summary>
        /// Command ID.
        /// </summary>
        public const int CommandId = 0x0100;

        /// <summary>
        /// Command menu group (command set GUID).
        /// </summary>
        public static readonly Guid CommandSet = new Guid("224d1ac0-74a3-4f63-9759-b5a7ca5b6681");

        /// <summary>
        /// VS Package that provides this command, not null.
        /// </summary>
        private readonly Package package;
        
        private const string CMFILE = "CMakeLists.txt";
        private const string CMDIR = "CMakeFiles";
        private const string CMPANE = "CMakeList Generator";
        private static string[] CMPROJS = new string[] { "ALL_BUILD", "ZERO_CHECK", "RUN_TESTS", "INSTALL" };

        private enum EConfType
        {
            Unknown = -1,
            Application = 1,
            DynamicLibrary = 2,
            StaticLibrary = 4
        }

        class ProjInfo
        {
            public string Name;
            public string Directory;
            public EConfType ProjType;
            public List<string> ProjDependencyNames = new List<string>();
        }

        private static string ToKey(string s)
        {
            string response = s
                .Replace(Path.DirectorySeparatorChar, '_')
                .Replace(' ', '_')
                .Replace('.', '_')
                .ToUpperInvariant();

            return response;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CMakeListsTxtCommand"/> class.
        /// Adds our command handlers for menu (commands must exist in the command table file)
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        private CMakeListsTxtCommand(Package package)
        {
            if (package == null)
            {
                throw new ArgumentNullException("package");
            }

            this.package = package;

            OleMenuCommandService commandService = this.ServiceProvider.GetService(typeof(IMenuCommandService)) as OleMenuCommandService;
            if (commandService != null)
            {
                var menuCommandID = new CommandID(CommandSet, CommandId);
                var menuItem = new MenuCommand(this.MenuItemCallback, menuCommandID);
                commandService.AddCommand(menuItem);
            }            
        }

        /// <summary>
        /// Gets the instance of the command.
        /// </summary>
        public static CMakeListsTxtCommand Instance
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the service provider from the owner package.
        /// </summary>
        private IServiceProvider ServiceProvider
        {
            get
            {
                return this.package;
            }
        }

        /// <summary>
        /// Initializes the singleton instance of the command.
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        public static void Initialize(Package package)
        {
            Instance = new CMakeListsTxtCommand(package);
        }

        /// <summary>
        /// This function is the callback used to execute the command when the menu item is clicked.
        /// See the constructor to see how the menu item is associated with this function using
        /// OleMenuCommandService service and MenuCommand class.
        /// </summary>
        /// <param name="sender">Event sender.</param>
        /// <param name="e">Event args.</param>
        private void MenuItemCallback(object sender, EventArgs e)
        {
            DTE dte = (DTE)this.ServiceProvider.GetService(typeof(DTE));

            Window window = dte.Windows.Item(EnvDTE.Constants.vsWindowKindOutput);
            OutputWindow outputWindow = (OutputWindow)window.Object;

            OutputWindowPane owp = null;
            foreach (OutputWindowPane opane in outputWindow.OutputWindowPanes)
            {
                if (string.Equals(CMPANE, opane.Name, StringComparison.InvariantCultureIgnoreCase))
                {
                    owp = opane;
                    break;
                }
            }
            owp = owp ?? outputWindow.OutputWindowPanes.Add(CMPANE);
            owp?.Activate();
            owp?.Clear();

            string slnDir = Path.GetDirectoryName(dte.Solution.FileName);
            string slnName = Path.GetFileNameWithoutExtension(dte.Solution.FullName);
            List<ProjInfo> projs = new List<ProjInfo>();
            {
                EConfType eConfigurationType = EConfType.Unknown;
                foreach (BuildDependency dep in dte.Solution.SolutionBuild.BuildDependencies)
                {
                    string pname = dep.Project.Name;
                    string pdir = Path.GetDirectoryName(dep.Project.FileName);
                    string ptype = dep.Project.Kind;

                    if (CMPROJS.Contains(pname))
                        continue;


                    ProjInfo pInf = new ProjInfo
                    {
                        Name = pname,
                        Directory = pdir
                    };
                    projs.Add(pInf);
                    owp?.OutputString($"Project: {pname} ({ptype}) @ {pdir}\n");

                    var depp = dep.RequiredProjects as System.Collections.IEnumerable;
                    foreach (Project rp in depp)
                    {
                        if (CMPROJS.Contains(rp.Name))
                            continue;

                        pInf.ProjDependencyNames.Add(rp.Name);
                        owp?.OutputString($"\tDep: {rp.Name}\n");
                    }

                    Configuration cfg = dep.Project.ConfigurationManager.ActiveConfiguration;
                    foreach (Property prop in cfg.Properties)
                    {
                        if (string.Equals("ConfigurationType", prop.Name, StringComparison.InvariantCultureIgnoreCase))
                        {
                            eConfigurationType = (EConfType)Enum.Parse(typeof(EConfType), prop.Value.ToString());
                            pInf.ProjType = eConfigurationType;
                            owp?.OutputString($"\tProp: {prop.Name} = {prop.Value} ({eConfigurationType})\n");
                        }
                    }
                }
            }
            
            this.CreateSolutionCMakeList(slnName, slnDir, projs.Select(s => s.Name));
            this.CreateProjectsCMakeLists(slnDir, projs);
        }

        private void CreateSolutionCMakeList(string slnName, string slnDir, IEnumerable<string> projs)
        {
            using (StreamWriter sw = new StreamWriter(Path.Combine(slnDir, CMFILE)))
            {
                sw.WriteLine("cmake_minimum_required (VERSION 3.0)");
                sw.WriteLine($"project ({slnName})");
                sw.WriteLine("set_property(GLOBAL PROPERTY USE_FOLDERS ON)");
                sw.WriteLine();
                foreach (string projName in projs)
                {
                    sw.WriteLine($"add_subdirectory ({projName})");
                }
            }
        }

        private void CreateProjectsCMakeLists(string slnDir, List<ProjInfo> projs)
        {
            foreach(ProjInfo p in projs)
            {
                using (StreamWriter sw = new StreamWriter(Path.Combine(p.Directory, CMFILE)))
                {
                    sw.WriteLine("cmake_minimum_required (VERSION 3.0)");
                    sw.WriteLine();

                    int iDirLen = p.Directory.Length;
                    if (!p.Directory.EndsWith(Path.DirectorySeparatorChar.ToString()))
                        iDirLen++;

                    List<string> sKeys = new List<string>();

                    Directory.EnumerateFiles(
                        p.Directory,
                        "*.*",
                        SearchOption.AllDirectories
                        )
                        .Where(w => w.EndsWith(".cpp") || w.EndsWith(".h"))
                        .Select(s => s.Substring(iDirLen))
                        .Where(w => !w.StartsWith(CMDIR))
                        .Select(s => new KeyValuePair<string, string>(ToKey(s), s))
                        .ToList()
                        .ForEach(f =>
                        {
                            string linPath = f.Value.Replace('\\', '/');
                            string grpPath = Path.GetDirectoryName(f.Value).Replace("\\", "\\\\");
                            sw.WriteLine($"set ({f.Key} \"{linPath}\")");
                            sw.WriteLine($"source_group(\"{grpPath}\" FILES ${{{f.Key}}})");
                            sKeys.Add($"${{{f.Key}}}");
                        });

                    sw.WriteLine();

                    if (p.ProjDependencyNames.Count > 0)
                    {
                        sw.WriteLine("# Properties->C/C++->General->Additional Include Directories");
                        foreach(string sDepProjName in p.ProjDependencyNames)
                        {
                            ProjInfo pDep = projs.FirstOrDefault(f => string.Equals(f.Name, sDepProjName, StringComparison.InvariantCultureIgnoreCase));
                            if (null == pDep) continue;

                            int iSlnDirLength = slnDir.Length;
                            if (!slnDir.EndsWith(Path.DirectorySeparatorChar.ToString()))
                                iSlnDirLength++;

                            string sRelDepProjPath = pDep.Directory
                                .Substring(iSlnDirLength)
                                .Replace('\\', '/');
                            sw.WriteLine($"include_directories (\"${{PROJECT_SOURCE_DIR}}/{sRelDepProjPath}\")");
                        }
                    }

                    sw.WriteLine();
                    sw.WriteLine("# Properties->C/C++->General->Additional Include Directories");
                    sw.WriteLine("include_directories (.)");

                    sw.WriteLine();
                    sw.WriteLine($"# Set Properties->General->Configuration Type to {p.ProjType}");
                    if (EConfType.Application == p.ProjType)
                        sw.Write($"add_executable ({p.Name}");
                    else if (EConfType.DynamicLibrary == p.ProjType)
                        sw.Write($"add_library({p.Name} DYNAMIC");
                    else if (EConfType.StaticLibrary == p.ProjType)
                        sw.Write($"add_library({p.Name} STATIC");

                    sw.WriteLine($" {string.Join(" ", sKeys)})");
                    sw.WriteLine();
                    sw.WriteLine("if(WIN32)");
                    sw.WriteLine("set (CMAKE_CXX_FLAGS \"${CMAKE_CXX_FLAGS} /W4\")");
                    sw.WriteLine("endif(WIN32)");
                    sw.WriteLine();
                    sw.WriteLine("if(UNIX)");
                    sw.WriteLine("set (CMAKE_CXX_FLAGS \"${CMAKE_CXX_FLAGS} -std=c++11\")");
                    sw.WriteLine("endif(UNIX)");

                    sw.WriteLine();
                    sw.WriteLine($"set_property(TARGET {p.Name} PROPERTY FOLDER \"{(EConfType.Application == p.ProjType ? "apps" : "libs")}\")");
                    sw.WriteLine($"set_property(TARGET {p.Name} PROPERTY CXX_STANDARD 11)"); // Cmake 3.1.3+

                    if (p.ProjDependencyNames.Count > 0)
                    {
                        sw.WriteLine();
                        sw.WriteLine("# Properties->Linker->Input->Additional Dependencie");
                        sw.WriteLine($"target_link_libraries ({p.Name} {string.Join(" ", p.ProjDependencyNames)})");
                    }
                }
            }
        }
    }
}
