using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
[assembly: AssemblyTitle("NavHelper.AssemblyResolver")]
[assembly: AssemblyVersion("1.0.0.1")]
[assembly: AssemblyFileVersion("1.0.0.1")]

namespace NavHelper.AssemblyResolver
{
    /// <summary>
    /// Helper class for Microsoft Dynamics NAV that allows C/AL to receive the AssemblyResolve event and process it at runtime.
    /// The class subscribes to the AssemblyResolve event of the current application domain at startup, and upon disposal it
    /// unsubscribes from events.
    /// The class uses a common assembly repository that will be created once per service tier instance runtime, but each session
    /// will be able to resolve an assembly independently. Assemblies resolved in one session will be available to other sessions.
    /// </summary>
    public class AssemblyResolver : IDisposable
    {
        private static readonly ConcurrentDictionary<string, Tuple<byte[], byte[]>> Assemblies =
            new ConcurrentDictionary<string, Tuple<byte[],byte[]>>();

        private static readonly List<AssemblyResolver> Resolvers = new List<AssemblyResolver>();
        private static bool _resolverActive;
        private static readonly object LockResolvers = new object();

        private bool _subscribed;


        private string GetExceptionMessage(Exception e, string method)
        {
            return string.Format("Exception of type {0} with message {1} was thrown in method {2}",
                e.GetType(), e.Message, method);
        }
        /// <summary>
        /// Subscribes this instance of AssemblyResolver to the AssemblyResolve event of the current application domain if
        /// no other instance of AssemblyResolver is already subscribed to that event.
        /// </summary>
        private void Subscribe()
        {
            try
            {
                if (_resolverActive) return;

                _resolverActive = true;
                _subscribed = true;
                AppDomain.CurrentDomain.AssemblyResolve += AssemblyResolve;
            }
            catch (Exception e)
            {
                throw new Exception(GetExceptionMessage(e, "Subscribe"));
            }
        }

        /// <summary>
        /// Unsubscribes this instance of AssemblyResolver from the AssemblyResolve event of the current application domain if
        /// this instance was subscribed to it.
        /// </summary>
        private void Unsubscribe()
        {
            try
            {
                if (!_subscribed) return;

                _resolverActive = false;
                AppDomain.CurrentDomain.AssemblyResolve -= AssemblyResolve;
            }
            catch (Exception e)
            {
                throw new Exception(GetExceptionMessage(e, "Unsubscribe"));
            }
        }

        private Assembly AssemblyResolve(object sender, ResolveEventArgs args)
        {
            try
            {
                if (!Assemblies.ContainsKey(args.Name) && OnResolveAssembly != null)
                    OnResolveAssembly.Invoke(sender, args);
            }
            catch (Exception e)
            {
                throw new Exception(GetExceptionMessage(e, "AssemblyResolve (resolving)"));
            }

            try
            {
                return AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.FullName == args.Name) ??
                       (Assemblies.ContainsKey(args.Name)
                           ? Assembly.Load(Assemblies[args.Name].Item1, Assemblies[args.Name].Item2)
                           : null);
            }
            catch (Exception e)
            {
                throw new Exception(GetExceptionMessage(e, "AssemblyResolve (retrieving)"));
            }
        }

        /// <summary>
        /// Constructor. It adds this instance of the AssemblyResolver to the static list of resolvers, and then attempts to
        /// subscribe this instance to the AssemblyResolve event of the current application domain.
        /// </summary>
        public AssemblyResolver()
        {
            lock (LockResolvers)
            {
                try
                {
                    Resolvers.Add(this);
                }
                catch (Exception e)
                {
                    throw new Exception(GetExceptionMessage(e, "[constructor]"));
                }
                Subscribe();
            }
        }

        /// <summary>
        /// The event that is be published to C/AL. It is simply a wrapper around the AssemblyResolve event that cannot be
        /// published to C/AL directly due to its incompatible signature.
        /// </summary>
        public event EventHandler<ResolveEventArgs> OnResolveAssembly;

        /// <summary>
        /// Stores the assembly in the instance cache.
        /// </summary>
        /// <param name="name">Fully qualified name of the assembly to store in the instance cache.</param>
        /// <param name="asm">Byte array containing the assembly bytes to store in the instance cache.</param>
        /// <param name="pdb">Byte array containing the debug information for the assembly.</param>
        public void ResolveAssembly(string name, byte[] asm, byte[] pdb)
        {
            try
            {
                Assemblies.AddOrUpdate(name, new Tuple<byte[], byte[]>(asm, pdb), (key, old) => new Tuple<byte[], byte[]>(asm, pdb));
            }
            catch (Exception e)
            {
                throw new Exception(GetExceptionMessage(e, "ResolveAssembly"));
            }
        }

        /// <summary>
        /// Implements the IDisposable interface. It first unsubscribes current instance from listening to the ApplicationResolve
        /// event of the current application domain (if this instance was subscribed to it), then removes this instance from the
        /// static list of AssemblyResolver instances, and then subscribes the next instance from the list if there are any left.
        /// This makes sure that there is always one and only one instance of the AssemblyResolver subscribed to the AssemblyResolve
        /// event of the current application domain, and that any instances belonging to ended sessions are unsubscribed so that
        /// they may be garbage-collected.
        /// </summary>
        void IDisposable.Dispose()
        {
            lock (LockResolvers)
            {
                Unsubscribe();

                try
                {
                    Resolvers.Remove(this);
                    if (Resolvers.Any())
                        Resolvers.First().Subscribe();
                }
                catch (Exception e)
                {
                    throw new Exception(GetExceptionMessage(e, "Dispose"));
                }
            }
        }
    }
}