using System;
using System.Collections.Generic;
using System.Reflection;

namespace Vjeko.Com.AssemblyResolver
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
        /// <summary>
        /// The static cache makes sure that there is only one repository of resolvable assemblies per application domain.
        /// </summary>
        private static readonly Dictionary<string, byte[]> Assemblies = new Dictionary<string, byte[]>();

        private bool _subscribed;
        private static readonly object LockAssemblies = new object();

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
        public void ResolveAssembly(string name, byte[] asm)
        {
            lock (LockAssemblies)
            {
                if (Assemblies.ContainsKey(name))
                    Assemblies.Remove(name);

                Assemblies.Add(name, asm);
            }
        }

        /// <summary>
        /// Constructor, creates an instance of the class and immediately subscribes to events if requested.
        /// </summary>
        /// <param name="autoSubscribe">If true, the instance will automatically subscribe to AssemblyResolve event.</param>
        public AssemblyResolver(bool autoSubscribe)
        {
            if (autoSubscribe)
                Subscribe();
        }

        /// <summary>
        /// Subscribes this instance to the AssemblyResolve event of the current application domain. It needs only be called
        /// if the instance is constructed with autoSubscribe = false.
        /// </summary>
        public void Subscribe()
        {
            if (!_subscribed)
            {
                AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;
                _subscribed = true;
            }
        }

        /// <summary>
        /// Unsubscribes this instance from the AssemblyResolve event of the current application domain. It should be called
        /// explicitly at the end of each session.
        /// </summary>
        public void Unsubscribe()
        {
            if (_subscribed)
            {
                AppDomain.CurrentDomain.AssemblyResolve -= CurrentDomain_AssemblyResolve;
                _subscribed = false;
            }
        }

        private Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            lock (LockAssemblies)
            {
                if (!Assemblies.ContainsKey(args.Name) && OnResolveAssembly != null)
                    OnResolveAssembly.Invoke(sender, args);

                return Assemblies.ContainsKey(args.Name) ? Assembly.Load(Assemblies[args.Name]) : null;
            }
        }

        /// <summary>
        /// Implements the Dispose method of the IDisposable interface. It provides the disposal logic in case Unsubscribe was
        /// not called directly, and the session ends (e.g. a session crash or something). This is just a safety mechanism in
        /// case there are issues with NAV properly disposing of an instance of AssemblyResolver class.
        /// </summary>
        void IDisposable.Dispose()
        {
            Unsubscribe();
        }
    }
}