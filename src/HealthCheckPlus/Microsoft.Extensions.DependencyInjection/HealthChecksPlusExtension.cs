// ********************************************************************************************
// MIT LICENCE
// The maintenance and evolution is maintained by the HealthCheckPlus project under MIT license
// ********************************************************************************************

using HealthCheckPlus.Abstractions;
using HealthCheckPlus.Internal;
using HealthCheckPlus.Internal.Policies;
using HealthCheckPlus.Internal.WrapperMicrosoft;
using HealthCheckPlus.Options;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Microsoft.Extensions.DependencyInjection
#pragma warning restore IDE0130 // Namespace does not match folder structure
{
    /// <summary>
    /// HealthChecksPlus Extension for DependencyInjection
    /// </summary>
    public static class HealthChecksPlusExtension
    {
        // Retrieves (or creates and registers) the HealthChecksPlusRegistrationState instance
        // scoped to this specific IServiceCollection. See HealthChecksPlusRegistrationState for
        // why registration-time state must be per-collection rather than process-wide.
        private static HealthChecksPlusRegistrationState GetOrCreateState(IServiceCollection services)
        {
            var descriptor = services.FirstOrDefault(x => x.ServiceType == typeof(HealthChecksPlusRegistrationState));
            if (descriptor?.ImplementationInstance is HealthChecksPlusRegistrationState existing)
            {
                return existing;
            }

            var state = new HealthChecksPlusRegistrationState();
            services.AddSingleton(state);
            return state;
        }

        /// <summary>
        /// Register HealthChecksPlus Background service with <see cref="HealthCheckPlusBackGroundOptions"/> options.
        /// <br>Default Values:</br>
        /// <br>Delay = 5 seconds.</br>
        /// <br>HealthyPeriod = 30 seconds.</br>
        /// <br>DegradedPeriod = 30 seconds.</br>
        /// <br>UnhealthyPeriod = 30 seconds.</br>
        /// <br>Timeout = 30 seconds.</br>
        /// <br>Idle = 1 second.</br>
        /// <br>Predicate = All HealthCheck.</br>
        /// </summary>
        /// <remarks>
        /// Call this after every health check registration (native, third-party, or via <see cref="AddCheckPlus{T}"/>/<see cref="AddCheckLinkTo"/>).
        /// This method removes the native <c>HealthCheckPublisherHostedService</c> so publishers aren't driven twice, but that removal only
        /// affects whatever is registered at the moment it runs - a later call to <c>IServiceCollection.AddHealthChecks()</c> (the app itself, or
        /// a third-party <see cref="IHealthChecksBuilder"/> extension that calls it defensively) silently re-adds it. If that happens, the host
        /// fails fast with an <see cref="InvalidOperationException"/> when it starts, instead of publishers being driven twice or by the wrong service with no signal.
        /// </remarks>
        /// <param name="ihb">The <see cref="IHealthChecksBuilder"/>.</param>
        /// <param name="option">The options for HealthChecksPlus Background service. See <see cref="HealthCheckPlusBackGroundOptions"/>.</param>
        /// <returns>The <see cref="IHealthChecksBuilder"/>.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="ihb"/> is <c>null</c>.</exception>
        /// <exception cref="InvalidOperationException"><see cref="AddBackgroundPolicy"/> was already called once for the same <see cref="IServiceCollection"/>, or <c>AddHealthChecksPlus</c> was never called first.</exception>
        public static IHealthChecksBuilder AddBackgroundPolicy(this IHealthChecksBuilder ihb, Action<HealthCheckPlusBackGroundOptions>? option = null)
        {
            ArgumentNullException.ThrowIfNull(ihb);

            var state = GetOrCreateState(ihb.Services);
            if (!state.AddedHealthChecksPlus)
            {
                throw new InvalidOperationException("Invalid command. The HealthChecks must first be declared by the AddHealthChecksPlus command");
            }

            // AddHostedService below accumulates rather than replaces - a second AddBackgroundPolicy
            // call used to silently register a second HealthCheckPlusBackGroundService instance, so
            // two independent background loops would run concurrently: checks executed twice per
            // cycle, publishers dispatched twice, metrics double-counted, with nothing indicating
            // the mistake.
            if (state.AddedBackgroundPolicy)
            {
                throw new InvalidOperationException($"Invalid command. {nameof(AddBackgroundPolicy)} was already called - call it only once.");
            }
            state.AddedBackgroundPolicy = true;

            // Remove the native HealthCheckPublisherHostedService so publishers aren't driven both
            // by it and by HealthCheckPlusBackGroundService. Unlike the HealthCheckService removal
            // in AddHealthChecksPlus, this one genuinely cannot be matched by a public service or
            // implementation type: it's registered as ServiceDescriptor.Singleton<IHostedService,
            // HealthCheckPublisherHostedService>() (internal sealed, implements only IHostedService
            // — no distinguishing public marker), and matching on ServiceType == typeof(IHostedService)
            // alone would risk removing unrelated hosted services the consumer's own app registered
            // (background workers, SignalR, etc.). Matching the fully qualified internal type name
            // (not just the short name) is the narrowest correct option available. If a future .NET
            // version renames/moves this type, this silently stops matching — the native and
            // HealthCheckPlus background services would then both run and both invoke publishers.
            // Known residual risk; no public replacement exists, unlike the HealthCheckService case.
            ServiceDescriptor? hcs = ihb.Services.FirstOrDefault(x =>
                x.ImplementationType?.FullName == "Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckPublisherHostedService");
            if (hcs != null)
            {
                ihb.Services.Remove(hcs!);
            }

#pragma warning disable CA1806 // Do not ignore method results
            option ??= (_) => new HealthCheckPlusBackGroundOptions();
#pragma warning restore CA1806 // Do not ignore method results
            ihb.Services.Configure(option);
            ihb.Services.AddHostedService<HealthCheckPlusBackGroundService>();
            return ihb;
        }


#pragma warning disable CS0419 // Ambiguous reference in cref attribute
        /// <summary>
        /// Register Unhealthy Policy for the health check
        /// </summary>
        /// <param name="ihb">The <see cref="IHealthChecksBuilder"/>.</param>
        /// <param name="namedep">The name health check to run.</param>
        /// <param name="period">
        /// Required <see cref="TimeSpan"/> The period of execution when status is Unhealthy.
        /// </param>
        /// <remarks>
        /// The <see cref="AddUnhealthyPolicy"/> cannot be set to a value lower than 1 second.
        /// </remarks>
        /// <returns>The <see cref="IHealthChecksBuilder"/>.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="ihb"/> or <paramref name="namedep"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException"><paramref name="namedep"/> is empty, or <paramref name="period"/> is below one second.</exception>
        /// <exception cref="InvalidOperationException"><c>AddHealthChecksPlus</c> was never called first.</exception>
        public static IHealthChecksBuilder AddUnhealthyPolicy(this IHealthChecksBuilder ihb, string namedep, TimeSpan period)
#pragma warning restore CS0419 // Ambiguous reference in cref attribute
        {
            ArgumentNullException.ThrowIfNull(ihb);
            ArgumentException.ThrowIfNullOrEmpty(namedep);

            if (!GetOrCreateState(ihb.Services).AddedHealthChecksPlus)
            {
                throw new InvalidOperationException("Invalid command. The HealthChecks must first be declared by the AddHealthChecksPlus command");
            }
            PeriodValidation.EnsureAtLeastOneSecond(period, nameof(period), nameof(period));
            ihb.Services.AddSingleton<HealthCheckPlusPolicyStatus>(new HealthCheckPlusPolicyStatus(HealthStatus.Unhealthy, TimeSpan.Zero, period, namedep));
            return ihb;
        }


#pragma warning disable CS0419 // Ambiguous reference in cref attribute
        /// <summary>
        /// Register Degraded Policy for the health check
        /// </summary>
        /// <param name="ihb">The <see cref="IHealthChecksBuilder"/>.</param>
        /// <param name="namedep">The name health check to run.</param>
        /// <param name="period">Required <see cref="TimeSpan"/>. The period of execution when status is Degraded.</param>
        /// <remarks>
        /// The <see cref="AddDegradedPolicy"/> cannot be set to a value lower than 1 second.
        /// </remarks>
        /// <returns>The <see cref="IHealthChecksBuilder"/>.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="ihb"/> or <paramref name="namedep"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException"><paramref name="namedep"/> is empty, or <paramref name="period"/> is below one second.</exception>
        /// <exception cref="InvalidOperationException"><c>AddHealthChecksPlus</c> was never called first.</exception>
        public static IHealthChecksBuilder AddDegradedPolicy(this IHealthChecksBuilder ihb, string namedep, TimeSpan period)
#pragma warning restore CS0419 // Ambiguous reference in cref attribute
        {
            ArgumentNullException.ThrowIfNull(ihb);
            ArgumentException.ThrowIfNullOrEmpty(namedep);

            if (!GetOrCreateState(ihb.Services).AddedHealthChecksPlus)
            {
                throw new InvalidOperationException("Invalid command. The HealthChecks must first be declared by the AddHealthChecksPlus command");
            }
            PeriodValidation.EnsureAtLeastOneSecond(period, nameof(period), nameof(period));

            ihb.Services.AddSingleton<HealthCheckPlusPolicyStatus>(new HealthCheckPlusPolicyStatus(HealthStatus.Degraded, TimeSpan.Zero, period, namedep));
            return ihb;
        }

        /// <summary>
        /// Register HealthChecksPlus Service. The set of tracked health checks is taken directly
        /// from whatever health checks end up registered (via <see cref="AddCheckPlus{T}"/>,
        /// <see cref="AddCheckLinkTo"/>, or any native <see cref="IHealthChecksBuilder"/>
        /// extension) by the time the service provider first resolves the check state - there is
        /// no separate name list to pass in or keep in sync. A check registered only through a
        /// native extension, with no <see cref="AddCheckPlus{T}"/>/<see cref="AddCheckLinkTo"/>
        /// Healthy policy of its own, still fails fast at startup with a clear error naming it.
        /// </summary>
        /// <param name="sc">The <see cref="IServiceCollection"/>.</param>
        /// <returns>The <see cref="IHealthChecksBuilder"/>.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="sc"/> is <c>null</c>.</exception>
        public static IHealthChecksBuilder AddHealthChecksPlus(this IServiceCollection sc)
        {
            ArgumentNullException.ThrowIfNull(sc);
            GetOrCreateState(sc).AddedHealthChecksPlus = true;

            IHealthChecksBuilder ihb = sc.AddHealthChecks();

            // Replace the HealthCheckService implementation registered by AddHealthChecks() with
            // DefaultHealthCheckServicePlus. Matched by the public HealthCheckService service type,
            // not by the internal DefaultHealthCheckService implementation type name — that
            // internal type is not a supported contract and has already been renamed/restructured
            // once across .NET versions.
            foreach (ServiceDescriptor descriptor in sc.Where(x => x.ServiceType == typeof(HealthCheckService)).ToArray())
            {
                sc.Remove(descriptor);
            }

            //add custom DefaultHealthCheckServicePlus
            sc.TryAddSingleton<IStateHealthChecksPlus>((sp) =>
            {
                // IOptions<HealthCheckServiceOptions>.Value is only evaluated here, on first
                // resolution of this singleton - by then every AddCheckPlus/AddCheckLinkTo/native
                // AddCheck call made anywhere during startup has already run, so the full set of
                // registered checks is already known. Seeding from it directly (instead of a
                // separately user-supplied names list) makes it impossible for the cache to
                // diverge from what was actually registered - the two constructor validations
                // that used to exist purely to catch that divergence (a name with no matching
                // registration, or vice versa) are gone because the failure mode they guarded
                // against can no longer happen.
                var registrations = sp.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations;
                CacheHealthCheckPlus inst = new(sp.GetService<ILogger<CacheHealthCheckPlus>>());
                inst.InitCache(registrations.Select(r => r.Name));
                return inst;
            });
            sc.TryAddSingleton<HealthCheckService, DefaultHealthCheckServicePlus>();
            return ihb;
        }

#pragma warning disable CS0419 // Ambiguous reference in cref attribute
        /// <summary>
        /// Registers a new health check to run.
        /// </summary>
        /// <param name="ihb">The <see cref="IHealthChecksBuilder"/>.</param>
        /// <param name="namedep">The name to register the health check under.</param>
        /// <param name="delay">An optional <see cref="TimeSpan"/> for this check's own <c>Healthy</c> policy - the initial delay before
        /// the check is first eligible to run, not a delay of any <see cref="IHealthCheckPublisher"/>. When omitted, the check has no
        /// delay on the HTTP path (it runs on the first request that reaches it); on the background path, an omitted delay falls back
        /// to <see cref="HealthCheckPlusBackGroundOptions.Delay"/> for that check's very first run only.</param>
        /// <param name="period">An optional <see cref="TimeSpan"/> for this check's own <c>Healthy</c> policy - how often it reruns,
        /// not a period of any <see cref="IHealthCheckPublisher"/>. On the HTTP path this also applies whenever the check's current
        /// status (Degraded/Unhealthy) has no explicit policy of its own registered via <see cref="AddDegradedPolicy"/>/
        /// <see cref="AddUnhealthyPolicy"/> - not only while the check is genuinely <c>Healthy</c>. When omitted, the check reruns on
        /// every request on the HTTP path; on the background path, this period (or, if omitted, <see cref="HealthCheckPlusBackGroundOptions.HealthyPeriod"/>)
        /// is only ever consulted while the check is currently <c>Healthy</c> - a check currently Degraded/Unhealthy uses
        /// <see cref="HealthCheckPlusBackGroundOptions.DegradedPeriod"/>/<see cref="HealthCheckPlusBackGroundOptions.UnhealthyPeriod"/> instead, unless an
        /// explicit <see cref="AddDegradedPolicy"/>/<see cref="AddUnhealthyPolicy"/> period was registered for it.</param>
        /// <param name="tags">A list of tags that can be used for filtering health checks.</param>
        /// <param name="failureStatus">
        /// The <see cref="HealthStatus"/> that should be reported when the health check reports a failure. If the provided value
        /// is <c>null</c>, then <see cref="HealthStatus.Unhealthy"/> will be reported.
        /// </param>
        /// <param name="timeout">An optional <see cref="TimeSpan"/> representing the timeout of the check.</param>
        /// <remarks>
        /// The <see cref="AddCheckPlus{T}"/> cannot be set to a period value lower than 1 second.
        /// </remarks>
        /// <returns>The <see cref="IHealthChecksBuilder"/>.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="ihb"/> or <paramref name="namedep"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException"><paramref name="namedep"/> is empty, or <paramref name="period"/> is provided and below one second.</exception>
        /// <exception cref="InvalidOperationException"><c>AddHealthChecksPlus</c> was never called first.</exception>
        public static IHealthChecksBuilder AddCheckPlus<T>(this IHealthChecksBuilder ihb, string namedep, TimeSpan? delay = null, TimeSpan? period = null, IEnumerable<string>? tags = null, HealthStatus? failureStatus = null, TimeSpan? timeout = null) where T : IHealthCheck
#pragma warning restore CS0419 // Ambiguous reference in cref attribute
        {
            ArgumentNullException.ThrowIfNull(ihb);
            ArgumentException.ThrowIfNullOrEmpty(namedep);

            if (!GetOrCreateState(ihb.Services).AddedHealthChecksPlus)
            {
                throw new InvalidOperationException("Invalid command. The HealthChecks must first be declared by the AddHealthChecksPlus command");
            }
            if (period.HasValue)
            {
                PeriodValidation.EnsureAtLeastOneSecond(period.Value, nameof(period), nameof(period));
            }

            HealthCheckRegistration reg = new(
                    namedep,
                    (sp) =>
                    {
                        return ActivatorUtilities.GetServiceOrCreateInstance<T>(sp);
                    },
                    failureStatus,
                    tags, timeout)
            {
                Delay = delay,
                Period = period
            };
            ihb.Add(reg);

            //add policy for Healthy
            ihb.Services.AddSingleton<HealthCheckPlusPolicyStatus>(
                new HealthCheckPlusPolicyStatus(HealthStatus.Healthy, reg.Delay, reg.Period, namedep));

            return ihb;
        }

#pragma warning disable CS0419 // Ambiguous reference in cref attribute
        /// <summary>
        /// Adopts an already-registered health check (e.g. one added by an external package's own <see cref="IHealthChecksBuilder"/> extension) so it can carry a HealthCheckPlus policy.
        /// </summary>
        /// <param name="ihb">The <see cref="IHealthChecksBuilder"/>.</param>
        /// <param name="namedep">The name to register the adopted health check under.</param>
        /// <param name="name">The name of the already-registered health check to adopt. This parameter is case-insensitive.</param>
        /// <param name="delay">An optional <see cref="TimeSpan"/> for the adopted check's own <c>Healthy</c> policy - the initial delay
        /// before it is first eligible to run, not a delay of any <see cref="IHealthCheckPublisher"/>. When omitted, the check has no
        /// delay on the HTTP path (it runs on the first request that reaches it); on the background path, an omitted delay falls back
        /// to <see cref="HealthCheckPlusBackGroundOptions.Delay"/> for that check's very first run only.</param>
        /// <param name="period">An optional <see cref="TimeSpan"/> for the adopted check's own <c>Healthy</c> policy - how often it
        /// reruns, not a period of any <see cref="IHealthCheckPublisher"/>. On the HTTP path this also applies whenever the check's
        /// current status (Degraded/Unhealthy) has no explicit policy of its own registered via <see cref="AddDegradedPolicy"/>/
        /// <see cref="AddUnhealthyPolicy"/> - not only while the check is genuinely <c>Healthy</c>. When omitted, the check reruns on
        /// every request on the HTTP path; on the background path, this period (or, if omitted, <see cref="HealthCheckPlusBackGroundOptions.HealthyPeriod"/>)
        /// is only ever consulted while the check is currently <c>Healthy</c> - a check currently Degraded/Unhealthy uses
        /// <see cref="HealthCheckPlusBackGroundOptions.DegradedPeriod"/>/<see cref="HealthCheckPlusBackGroundOptions.UnhealthyPeriod"/> instead, unless an
        /// explicit <see cref="AddDegradedPolicy"/>/<see cref="AddUnhealthyPolicy"/> period was registered for it.</param>
        /// <remarks>
        /// <para>The <see cref="AddCheckLinkTo"/> cannot be set to a period value lower than 1 second.</para>
        /// <para>
        /// Must be called after the check named <paramref name="name"/> is itself registered (e.g. via a
        /// third-party package's own <see cref="IHealthChecksBuilder"/> extension such as <c>AddRedis</c>) -
        /// it adopts an existing registration rather than creating one. Calling it first throws at the point
        /// the options are resolved (typically at startup, when the service provider is built), not from this
        /// method itself.
        /// </para>
        /// </remarks>
        /// <returns>The <see cref="IHealthChecksBuilder"/>.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="ihb"/>, <paramref name="namedep"/>, or <paramref name="name"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException"><paramref name="namedep"/> or <paramref name="name"/> is empty, <paramref name="namedep"/> equals <paramref name="name"/>, or <paramref name="period"/> is provided and below one second.</exception>
        /// <exception cref="InvalidOperationException"><c>AddHealthChecksPlus</c> was never called first. Separately, resolving
        /// <see cref="Microsoft.Extensions.Options.IOptions{HealthCheckServiceOptions}"/> throws this same exception type if no
        /// check named <paramref name="name"/> was ever registered.</exception>
        public static IHealthChecksBuilder AddCheckLinkTo(this IHealthChecksBuilder ihb, string namedep, string name, TimeSpan? delay = null, TimeSpan? period = null)
#pragma warning restore CS0419 // Ambiguous reference in cref attribute
        {
            ArgumentNullException.ThrowIfNull(ihb);
            ArgumentException.ThrowIfNullOrEmpty(namedep);
            ArgumentException.ThrowIfNullOrEmpty(name);

            if (!GetOrCreateState(ihb.Services).AddedHealthChecksPlus)
            {
                throw new InvalidOperationException("Invalid command. The HealthChecks must first be declared by the AddHealthChecksPlus command");
            }
            if (period.HasValue)
            {
                PeriodValidation.EnsureAtLeastOneSecond(period.Value, nameof(period), nameof(period));
            }

            if (namedep.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException($"'{namedep}' cannot be the same as the registered check name '{name}'.");
            }

            var state = GetOrCreateState(ihb.Services);

            // Adopt the existing registration named `name` (e.g. one added by a third-party
            // package's own IHealthChecksBuilder extension, such as AddRedis) by hooking into the
            // same public, documented Options pipeline that registration itself used to get there,
            // instead of reaching into internal ASP.NET Core types.
            //
            // Configure<HealthCheckServiceOptions> callbacks run in registration order against the
            // one, real HealthCheckServiceOptions instance when IOptions<HealthCheckServiceOptions>
            // is first resolved — so as long as AddCheckLinkTo is called after the original
            // registration (the documented usage), `name`'s HealthCheckRegistration is already
            // present in `options.Registrations` by the time this callback runs. Deliberately not
            // done by scanning ServiceDescriptor.ImplementationInstance for a
            // ConfigureNamedOptions<HealthCheckServiceOptions> and reflecting into its captured
            // Action to reconstruct a throwaway copy of the options — that would reach into an
            // internal ASP.NET Core type with no supported contract.
            ihb.Services.Configure<HealthCheckServiceOptions>(options =>
            {
                HealthCheckRegistration? original = options.Registrations
                    .FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

                if (original is null)
                {
                    throw new InvalidOperationException(
                        $"No health check named '{name}' was found. Register it (e.g. via the third-party package's own " +
                        $"IHealthChecksBuilder extension) before calling {nameof(AddCheckLinkTo)}.");
                }

                options.Registrations.Remove(original);

                HealthCheckRegistration reg = new(
                        namedep,
                        (sp) => state.ExternalCheck.GetOrAdd(namedep, _ => new Lazy<WrapperBaseHealthCheckPlus>(() =>
                        {
                            // Build the adopted check from a scope created and owned here, not the
                            // caller's own per-execution scope (DefaultHealthCheckServicePlus.
                            // RunCheckAsync disposes that one right after this factory returns).
                            // The adopted check instance is cached and reused for the process's
                            // entire lifetime (see ExternalCheck above), so any scoped dependency it
                            // resolves during construction (e.g. the native AddDbContextCheck<T>'s
                            // DbContext) must stay alive that whole time too - not just for its
                            // first execution. This owned scope is disposed together with the
                            // wrapper in DefaultHealthCheckServicePlus.Dispose().
                            var ownedScope = sp.GetRequiredService<IServiceScopeFactory>().CreateScope();
                            try
                            {
                                return new WrapperBaseHealthCheckPlus(original.Factory(ownedScope.ServiceProvider), ownedScope);
                            }
                            catch
                            {
                                // original.Factory throwing must not leak the scope just created for
                                // it - the wrapper that would otherwise own (and dispose) it was never
                                // constructed. Known, accepted limitation: Lazy<T>'s default mode
                                // (deliberately kept - see ExternalCheck's own comment) caches this
                                // exception, so a failing construction disables this adopted check
                                // until the process restarts; retrying safely would require giving up
                                // the guarantee that two concurrent callers can never each construct a
                                // real instance.
                                ownedScope.Dispose();
                                throw;
                            }
                        })).Value,
                        original.FailureStatus,
                        original.Tags,
                        original.Timeout)
                {
                    Delay = delay,
                    Period = period
                };
                options.Registrations.Add(reg);
            });

            //add policy for Healthy
            ihb.Services.AddSingleton<HealthCheckPlusPolicyStatus>(
                new HealthCheckPlusPolicyStatus(HealthStatus.Healthy, delay, period, namedep));

            return ihb;
        }
    }
}
