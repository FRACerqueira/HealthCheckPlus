﻿using HealthCheckPlus;
using HealthCheckPlus.Internal;
using HealthCheckPlus.Internal.WrapperMicrosoft;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// HealthChecksPlus Extension for DependencyInjection
/// </summary>
public static class HealthChecksPlusExtension
{
    private static readonly ConcurrentDictionary<string, WrapperBaseHealthCheckPlus> _externalCheck = new();

    /// <summary>
    /// Register Unhealthy Policy for the health check
    /// </summary>
    /// <param name="ihb">The <see cref="IHealthChecksBuilder"/>.</param>
    /// <param name="enumdep">The <see cref="Enum"/> with health check to run.</param>
    /// <param name="period">Requeried <see cref="TimeSpan"/>. The period of execution when status is Unhealthy.</param>
    /// <returns>The <see cref="IHealthChecksBuilder"/>.</returns>
    public static IHealthChecksBuilder AddUnhealthyPolicy<T>(this IHealthChecksBuilder ihb, T enumdep, TimeSpan period) where T : Enum
    {
        ihb.Services.AddSingleton<IHealthCheckPlusUnhealthyPolicy>(new HealthCheckPlusUnhealthyPolicy(period, enumdep.ToString()));
        return ihb;
    }

    /// <summary>
    /// Register Degraded Policy for the health check
    /// </summary>
    /// <param name="ihb">The <see cref="IHealthChecksBuilder"/>.</param>
    /// <param name="enumdep">The <see cref="Enum"/> with health check to run.</param>
    /// <param name="period">Requeried <see cref="TimeSpan"/>. The period of execution when status is Unhealthy.</param>
    /// <returns>The <see cref="IHealthChecksBuilder"/>.</returns>
    public static IHealthChecksBuilder AddDegradedPolicy<T>(this IHealthChecksBuilder ihb, T enumdep, TimeSpan period) where T : Enum
    {
        ihb.Services.AddSingleton<IHealthCheckPlusDegradedPolicy>(new HealthCheckPlusDegradedPolicy(period, enumdep.ToString()));
        return ihb;
    }

    /// <summary>
    /// Register Aplication health check
    /// </summary>
    /// <typeparam name="T">Enum dependences</typeparam>
    /// <param name="sc">The <see cref="IServiceCollection"/>.</param>
    /// <param name="name">The health check name. Requeried.</param>
    /// <param name="delay">The initial delay applied after the application starts. The default value is 5 seconds.The min.value is 1 second.</param>
    /// <param name="period"> the period of <see cref="IHealthCheckPublisher"/> execution. The default value is 1 seconds. The min.value is 500 milesecond.</param>
    /// <param name="failureStatus">
    /// The <see cref="HealthStatus"/> that should be reported when the health check reports a failure. If the provided value
    /// is <c>null</c>, then <see cref="HealthStatus.Unhealthy"/> will be reported.
    /// </param>
    /// <param name="categorylog">An optional category name for logger.</param>
    /// <param name="actionlog">An optional action to write log.</param>
    /// <returns>The <see cref="IHealthChecksBuilder"/>.</returns>
    public static IHealthChecksBuilder AddHealthChecks<T>(this IServiceCollection sc, string name, TimeSpan? delay = null, TimeSpan? period = null, HealthStatus failureStatus =  HealthStatus.Unhealthy,string? categorylog = null, Action<ILogger, DataResutStatus> actionlog = null) where T : Enum
    {
        delay ??= TimeSpan.FromSeconds(5);
        if (delay.Value.Milliseconds < 1)
        {
            delay = TimeSpan.FromSeconds(1);
        }
        period ??= TimeSpan.FromSeconds(1);
        if (period.Value.TotalMilliseconds < 500)
        {
            period = TimeSpan.FromMilliseconds(500);
        }
        var tp = typeof(T);
        var enumValues = Enum.GetValues(tp);
        var aux = new List<string>();
        for (int i = 0; i < enumValues.Length; i++)
        {
            aux.Add(enumValues!.GetValue(i)!.ToString()!);
        }
        var lstdep = aux.ToArray();

        sc.Configure<HealthCheckPlusOptions>(options =>
        {
            options.NameCheckApp = name;    
            options.StatusFail = failureStatus;
            options.CategoryForLogger = categorylog;
            options.ActionForLog = actionlog;
        });

        sc.TryAddSingleton<IHealthCheckPublisher, PublishHealthCheckPlusApp<T>>();
        sc.Configure<HealthCheckPublisherOptions>(options =>
        {
            options.Predicate = hcr => lstdep.Any(x => x.Equals(hcr.Name, StringComparison.InvariantCulture));
            options.Delay = delay.Value;
            options.Period = period.Value;
        });

        var ihb = sc.AddHealthChecks();
        //remove Microsoft DefaultHealthCheckService (write log)
        var hcs = sc.FirstOrDefault(x => x.ImplementationType != null && x.ImplementationType.Name.Equals("DefaultHealthCheckService"));
        if (hcs != null)
        {
            sc.Remove(hcs!);
        }
        //add custom DefaultHealthCheckServicePlus (no write log)
        sc.TryAddSingleton<HealthCheckService, DefaultHealthCheckServicePlus>();

        ihb.Add(new HealthCheckRegistration(
                    name,
                    ActivatorUtilities.GetServiceOrCreateInstance<PublishHealthCheckPlusApp<T>>,
                    null, null, null));

        return ihb;
    }


    /// <summary>
    /// Register Aplication health check
    /// </summary>
    /// <typeparam name="T">Enum dependences</typeparam>
    /// <param name="sc">The <see cref="IServiceCollection"/>.</param>
    /// <param name="name">The health check name. Requeried.</param>
    /// <param name="failureStatus">
    /// The user function to reports <see cref="HealthStatus"/> .
    /// </param>
    /// <param name="delay">The initial delay applied after the application starts. The default value is 5 seconds.The min.value is 1 second.</param>
    /// <param name="period"> the period of <see cref="IHealthCheckPublisher"/> execution. The default value is 1 seconds. The min.value is 500 milesecond.</param>
    /// <param name="categorylog">An optional category name for logger.</param>
    /// <param name="actionlog">An optional action to write log.</param>
    /// <returns>The <see cref="IHealthChecksBuilder"/>.</returns>
    public static IHealthChecksBuilder AddHealthChecks<T>(this IServiceCollection sc, string name,  Func<IStateHealthChecksPlus,HealthStatus> failureStatus, TimeSpan? delay = null, TimeSpan? period = null, string? categorylog = null, Action<ILogger, DataResutStatus> actionlog = null) where T : Enum
    {
        delay ??= TimeSpan.FromSeconds(5);
        if (delay.Value.TotalMilliseconds < 1)
        {
            delay = TimeSpan.FromSeconds(1);
        }
        period ??= TimeSpan.FromSeconds(1);
        if (period.Value.TotalMilliseconds < 500)
        {
            period = TimeSpan.FromMilliseconds(500);
        }
        var tp = typeof(T);
        var enumValues = Enum.GetValues(tp);
        var aux = new List<string>();
        for (int i = 0; i < enumValues.Length; i++)
        {
            aux.Add(enumValues!.GetValue(i)!.ToString()!);
        }
        var lstdep = aux.ToArray();

        sc.Configure<HealthCheckPlusOptions>(options =>
        {
            options.NameCheckApp = name;
            options.StatusFail = HealthStatus.Unhealthy;
            options.FuncStatusFail = failureStatus;
            options.CategoryForLogger = categorylog;
            options.ActionForLog = actionlog;   
        });

        sc.TryAddSingleton<IHealthCheckPublisher, PublishHealthCheckPlusApp<T>>();

        sc.Configure<HealthCheckPublisherOptions>(options =>
        {
            options.Predicate = hcr => lstdep.Any(x => x.Equals(hcr.Name, StringComparison.InvariantCulture));
            options.Delay = delay.Value;
            options.Period = period.Value;
        });

        var ihb = sc.AddHealthChecks();
        //remove Microsoft DefaultHealthCheckService (write log)
        var hcs = sc.FirstOrDefault(x => x.ImplementationType != null && x.ImplementationType.Name.Equals("DefaultHealthCheckService"));
        if (hcs != null)
        {
            sc.Remove(hcs);
        }
        //add custom DefaultHealthCheckServicePlus (no write log)
        sc.TryAddSingleton<HealthCheckService, DefaultHealthCheckServicePlus>();

        ihb.Add(new HealthCheckRegistration(
                    name,
                    ActivatorUtilities.GetServiceOrCreateInstance<PublishHealthCheckPlusApp<T>>,
                    null, null, null));

        return ihb;
    }

    /// <summary>
    /// Register then dependence health check to run. the class health check class must inherit from <see cref="BaseHealthCheckPlus"/>.
    /// </summary>
    /// <param name="ihb">The <see cref="IHealthChecksBuilder"/>.</param>
    /// <param name="enumdep">The <see cref="Enum"/> with health check list to run.</param>
    /// <param name="delay">The initial delay applied after the application starts. The default value is 5 seconds.The min.value is 1 second.</param>
    /// <param name="period"> the period of <see cref="IHealthCheckPublisher"/> execution. The default value is 1 seconds. The min.value is 500 milesecond.</param>
    /// <param name="tags">A list of tags that can be used for filtering health checks.</param>
    /// <param name="failureStatus">
    /// The <see cref="HealthStatus"/> that should be reported when the health check reports a failure. If the provided value
    /// is <c>null</c>, then <see cref="HealthStatus.Unhealthy"/> will be reported.
    /// </param>
    /// <param name="timeout">An optional <see cref="TimeSpan"/> representing the timeout of the check.</param>
    /// <returns>The <see cref="IHealthChecksBuilder"/>.</returns>
    public static IHealthChecksBuilder AddCheckPlus<TE, T>(this IHealthChecksBuilder ihb, TE enumdep, TimeSpan? delay = null, TimeSpan? period = null, IEnumerable<string>? tags = null, HealthStatus? failureStatus = null, TimeSpan? timeout = null) where T : BaseHealthCheckPlus where TE : Enum
    {
        ihb.Services.TryAddSingleton<IStateHealthChecksPlus>((sp) => 
        {
            var opt = sp.GetRequiredService<IOptions<HealthCheckPlusOptions>>();
            return new StateHealthChecksPlus<TE>(opt);
        });
        ihb.Add(new HealthCheckRegistration(
                enumdep.ToString(),
                (sp) =>
                {
                    var staopt = (IStateHealthCheckPlusInternal)sp.GetRequiredService<IStateHealthChecksPlus>();
                    staopt.SetDelayInternal(enumdep.ToString(), delay ?? TimeSpan.FromSeconds(5), period ?? TimeSpan.FromSeconds(30));
                    return ActivatorUtilities.GetServiceOrCreateInstance<T>(sp);
                },
                failureStatus,
                tags, timeout));
        return ihb;
    }

    /// <summary>
    /// Register then external(package import) dependence health check to run. the health check must added in <see cref="IHealthChecksBuilder"/>.
    /// </summary>
    /// <param name="ihb">The <see cref="IHealthChecksBuilder"/>.</param>
    /// <param name="enumdep">The <see cref="Enum"/> with health check list to run.</param>
    /// <param name="name">The name health check registered. This param is case insensitive</param>
    /// <param name="delay">An optional <see cref="TimeSpan"/>. The initial delay applied after the application starts before executing
    /// <see cref="IHealthCheckPublisher"/> instances. The delay is applied once at startup, and does
    /// not apply to subsequent iterations. The default value is 5 seconds.</param>
    /// <param name="period">An optional <see cref="TimeSpan"/>. The period of <see cref="IHealthCheckPublisher"/> execution. The default value is 30 seconds</param>
    /// <param name="tags">A list of tags that can be used for filtering health checks.</param>
    /// <param name="failureStatus">
    /// The <see cref="HealthStatus"/> that should be reported when the health check reports a failure. If the provided value
    /// is <c>null</c>, then <see cref="HealthStatus.Unhealthy"/> will be reported.
    /// </param>
    /// <param name="timeout">An optional <see cref="TimeSpan"/> representing the timeout of the check.</param>
    /// <returns>The <see cref="IHealthChecksBuilder"/>.</returns>
    public static IHealthChecksBuilder AddCheckRegistered<TE>(this IHealthChecksBuilder ihb, TE enumdep, string name, TimeSpan? delay = null, TimeSpan? period = null, IEnumerable<string>? tags = null, HealthStatus? failureStatus = null, TimeSpan? timeout = null) where TE: Enum
    {
        if (enumdep.ToString().Equals(name, StringComparison.CurrentCultureIgnoreCase))
        {
            throw new ArgumentException($"Enum Name({enumdep}) has same name of registered check name({name}).");
        }

        var srvs_opt = ihb.Services
            .Where(x =>  x.ImplementationInstance != null && x.ServiceType.UnderlyingSystemType.FullName.Contains("HealthCheckServiceOptions"))
            .ToArray();

        foreach (var item in srvs_opt)
        {
            var hc_srvopt = (ConfigureNamedOptions<HealthCheckServiceOptions>)item.ImplementationInstance;
            var act_opt = new HealthCheckServiceOptions();
            hc_srvopt.Action.Invoke(act_opt);
            if (act_opt.Registrations.Any(x => x.Name.Equals(name, StringComparison.CurrentCultureIgnoreCase)))
            {
                ihb.Services.TryAddSingleton<IStateHealthChecksPlus, StateHealthChecksPlus<TE>>();
                ihb.Add(new HealthCheckRegistration(
                        enumdep.ToString(),
                        (sp) =>
                        {
                            if (_externalCheck.TryGetValue(enumdep.ToString(), out WrapperBaseHealthCheckPlus value))
                            {
                                return value;
                            }
                            //first check !save in dict.
                            var staopt = (IStateHealthCheckPlusInternal)sp.GetRequiredService<IStateHealthChecksPlus>();
                            staopt.SetDelayInternal(enumdep.ToString(), delay ?? TimeSpan.FromSeconds(5), period ?? TimeSpan.FromSeconds(30));
                            //ensure existed in dict.
                            _externalCheck.TryAdd(enumdep.ToString(), new WrapperBaseHealthCheckPlus(act_opt.Registrations.First().Factory.Invoke(sp), sp));
                            return _externalCheck[enumdep.ToString()];
                        },
                        failureStatus,
                        tags, timeout));
            }
        }
        return ihb;
    }
    
}
