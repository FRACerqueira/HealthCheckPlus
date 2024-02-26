// ********************************************************************************************
// MIT LICENCE
// The maintenance and evolution is maintained by the HealthCheckPlus project under MIT license
// ********************************************************************************************

using System.Collections.Concurrent;
using HealthCheckPlus.Abstractions;
using HealthCheckPlus.Internal;
using HealthCheckPlus.Internal.Policies;
using HealthCheckPlus.Internal.WrapperMicrosoft;
using HealthCheckPlus.options;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// HealthChecksPlus Extension for DependencyInjection
    /// </summary>
    public static class HealthChecksPlusExtension
    {
        private static readonly ConcurrentDictionary<string, WrapperBaseHealthCheckPlus> _externalCheck = new();
        private static Type? _enumType;

        /// <summary>
        /// Register HealthChecksPlus Background service with <see cref="HealthCheckPlusBackGroundOptions"/> options.
        /// <br>Default Values:</br>
        /// <br>Delay = 5 seconds.</br>
        /// <br>HealthyPeriod = 30 seconds.</br>
        /// <br>DegradedPeriod = 30 seconds.</br>
        /// <br>UnhealthyPeriod = 30 seconds.</br>
        /// <br>Timeout = 30 seconds.</br>
        /// <br>Predicate = All HealthCheck.</br>
        /// </summary>
        /// <param name="ihb">The <see cref="IHealthChecksBuilder"/>.</param>
        /// <param name="option">The options for HealthChecksPlus Background service. See <see cref="HealthCheckPlusBackGroundOptions"/>.</param>
        /// <returns>The <see cref="IHealthChecksBuilder"/>.</returns>
        public static IHealthChecksBuilder AddBackgroundPolicy(this IHealthChecksBuilder ihb, Action<HealthCheckPlusBackGroundOptions>? option = null)
        {
            ArgumentNullException.ThrowIfNull(ihb);

            if (_enumType is null)
            {
                throw new InvalidOperationException("Invalid command. The HealthChecks enum must first be declared by the AddHealthChecksPlus command");
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
        /// <param name="enumdep">The enum value with health check to run.</param>
        /// <param name="period">
        /// Requeried <see cref="TimeSpan"/> The period of execution when status is Unhealthy.
        /// </param>
        /// <remarks>
        /// The <see cref="AddUnhealthyPolicy"/> cannot be set to a value lower than 1 second.
        /// </remarks>
        /// <returns>The <see cref="IHealthChecksBuilder"/>.</returns>
        public static IHealthChecksBuilder AddUnhealthyPolicy(this IHealthChecksBuilder ihb, Enum enumdep, TimeSpan period)
#pragma warning restore CS0419 // Ambiguous reference in cref attribute
        {
            ArgumentNullException.ThrowIfNull(ihb);

            if (_enumType is null)
            {
                throw new InvalidOperationException("Invalid command. The HealthChecks enum must first be declared by the AddHealthChecksPlus command");
            }
            if (!enumdep.GetType().Equals(_enumType))
            {
                throw new ArgumentException($"The {nameof(enumdep)} must be of type {_enumType.Name}", nameof(enumdep));
            }
            if (period < TimeSpan.FromSeconds(1))
            {
                throw new ArgumentException($"The {nameof(period)} must be greater than or equal to one second.", nameof(period));
            }
            ihb.Services.AddSingleton<IHealthCheckPlusPolicyStatus>(new HealthCheckPlusPolicyStatus(HealthStatus.Unhealthy, TimeSpan.Zero, period, enumdep.ToString()));
            return ihb;
        }


#pragma warning disable CS0419 // Ambiguous reference in cref attribute
        /// <summary>
        /// Register Unhealthy Policy for the health check
        /// </summary>
        /// <param name="ihb">The <see cref="IHealthChecksBuilder"/>.</param>
        /// <param name="namedep">The name health check to run.</param>
        /// <param name="period">
        /// Requeried <see cref="TimeSpan"/> The period of execution when status is Unhealthy.
        /// </param>
        /// <remarks>
        /// The <see cref="AddUnhealthyPolicy"/> cannot be set to a value lower than 1 second.
        /// </remarks>
        /// <returns>The <see cref="IHealthChecksBuilder"/>.</returns>
        public static IHealthChecksBuilder AddUnhealthyPolicy(this IHealthChecksBuilder ihb, string namedep, TimeSpan period)
#pragma warning restore CS0419 // Ambiguous reference in cref attribute
        {
            ArgumentNullException.ThrowIfNull(ihb);

            if (_enumType is null)
            {
                throw new InvalidOperationException("Invalid command. The HealthChecks enum must first be declared by the AddHealthChecksPlus command");
            }
            if (period < TimeSpan.FromSeconds(1))
            {
                throw new ArgumentException($"The {nameof(period)} must be greater than or equal to one second.", nameof(period));
            }
            ihb.Services.AddSingleton<IHealthCheckPlusPolicyStatus>(new HealthCheckPlusPolicyStatus(HealthStatus.Unhealthy, TimeSpan.Zero, period, namedep));
            return ihb;
        }


#pragma warning disable CS0419 // Ambiguous reference in cref attribute
        /// <summary>
        /// Register Degraded Policy for the health check
        /// </summary>
        /// <param name="ihb">The <see cref="IHealthChecksBuilder"/>.</param>
        /// <param name="enumdep">The enum value with health check to run.</param>
        /// <param name="period">Requeried <see cref="TimeSpan"/>. The period of execution when status is Unhealthy.</param>
        /// <remarks>
        /// The <see cref="AddDegradedPolicy"/> cannot be set to a value lower than 1 second.
        /// </remarks>
        /// <returns>The <see cref="IHealthChecksBuilder"/>.</returns>
        public static IHealthChecksBuilder AddDegradedPolicy(this IHealthChecksBuilder ihb, Enum enumdep, TimeSpan period)
#pragma warning restore CS0419 // Ambiguous reference in cref attribute
        {
            ArgumentNullException.ThrowIfNull(ihb);

            if (_enumType is null)
            {
                throw new InvalidOperationException("Invalid command. The HealthChecks enum must first be declared by the AddHealthChecksPlus command");
            }
            if (!enumdep.GetType().Equals(_enumType))
            {
                throw new ArgumentException($"The {nameof(enumdep)} must be of type {_enumType.Name}", nameof(enumdep));
            }
            if (period < TimeSpan.FromSeconds(1))
            {
                throw new ArgumentException($"The {nameof(period)} must be greater than or equal to one second.", nameof(period));
            }

            ihb.Services.AddSingleton<IHealthCheckPlusPolicyStatus>(new HealthCheckPlusPolicyStatus(HealthStatus.Degraded, TimeSpan.Zero, period, enumdep.ToString()));
            return ihb;
        }


#pragma warning disable CS0419 // Ambiguous reference in cref attribute
        /// <summary>
        /// Register Degraded Policy for the health check
        /// </summary>
        /// <param name="ihb">The <see cref="IHealthChecksBuilder"/>.</param>
        /// <param name="namedep">The name health check to run.</param>
        /// <param name="period">Requeried <see cref="TimeSpan"/>. The period of execution when status is Unhealthy.</param>
        /// <remarks>
        /// The <see cref="AddDegradedPolicy"/> cannot be set to a value lower than 1 second.
        /// </remarks>
        /// <returns>The <see cref="IHealthChecksBuilder"/>.</returns>
        public static IHealthChecksBuilder AddDegradedPolicy(this IHealthChecksBuilder ihb, string namedep, TimeSpan period)
#pragma warning restore CS0419 // Ambiguous reference in cref attribute
        {
            ArgumentNullException.ThrowIfNull(ihb);

            if (_enumType is null)
            {
                throw new InvalidOperationException("Invalid command. The HealthChecks enum must first be declared by the AddHealthChecksPlus command");
            }
            if (period < TimeSpan.FromSeconds(1))
            {
                throw new ArgumentException($"The {nameof(period)} must be greater than or equal to one second.", nameof(period));
            }

            ihb.Services.AddSingleton<IHealthCheckPlusPolicyStatus>(new HealthCheckPlusPolicyStatus(HealthStatus.Degraded, TimeSpan.Zero, period, namedep));
            return ihb;
        }

        /// <summary>
        /// Register HealthChecksPlus Service
        /// </summary>
        /// <param name="sc">The <see cref="IServiceCollection"/>.</param>
        /// <param name="names">List of HealthChecks names</param>
        /// <returns>The <see cref="IHealthChecksBuilder"/>.</returns>
        public static IHealthChecksBuilder AddHealthChecksPlus(this IServiceCollection sc,IEnumerable<string> names)
        {
            _enumType = typeof(string);
            ArgumentNullException.ThrowIfNull(sc);
            ArgumentNullException.ThrowIfNull(names);
            if (!names.Any())
            {
                throw new ArgumentException("Not any List of HealthChecks names");
            }
            IHealthChecksBuilder ihb = sc.AddHealthChecks();
            //remove Microsoft DefaultHealthCheckService
            ServiceDescriptor? hcs = sc.FirstOrDefault(x => x.ImplementationType != null && x.ImplementationType.Name.Equals("DefaultHealthCheckService"));
            if (hcs != null)
            {
                sc.Remove(hcs!);
            }
            //add custom DefaultHealthCheckServicePlus
            sc.TryAddSingleton<IStateHealthChecksPlus>((_) =>
            {
                CacheHealthCheckPlus inst = new();
                inst.InitCache(names);
                return inst;
            });
            sc.TryAddSingleton<HealthCheckService, DefaultHealthCheckServicePlus>();
            return ihb;
        }

        /// <summary>
        /// Register HealthChecksPlus Service
        /// </summary>
        /// <typeparam name="T">Type Enum with all HealthChecks</typeparam>
        /// <param name="sc">The <see cref="IServiceCollection"/>.</param>
        /// <returns>The <see cref="IHealthChecksBuilder"/>.</returns>
        public static IHealthChecksBuilder AddHealthChecksPlus<T>(this IServiceCollection sc) where T : Enum
        {
            ArgumentNullException.ThrowIfNull(sc);

            _enumType = typeof(T);
            IHealthChecksBuilder ihb = sc.AddHealthChecks();
            //remove Microsoft DefaultHealthCheckService
            ServiceDescriptor? hcs = sc.FirstOrDefault(x => x.ImplementationType != null && x.ImplementationType.Name.Equals("DefaultHealthCheckService"));
            if (hcs != null)
            {
                sc.Remove(hcs!);
            }
            //add custom DefaultHealthCheckServicePlus
            sc.TryAddSingleton<IStateHealthChecksPlus>((_) =>
            {
                CacheHealthCheckPlus inst = new();
                inst.InitCache<T>();
                return inst;
            });
            sc.TryAddSingleton<HealthCheckService, DefaultHealthCheckServicePlus>();
            return ihb;
        }


#pragma warning disable CS0419 // Ambiguous reference in cref attribute
        /// <summary>
        /// Register then dependence health check to run.
        /// </summary>
        /// <param name="ihb">The <see cref="IHealthChecksBuilder"/>.</param>
        /// <param name="namedep">The name health check list to run.</param>
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
        /// <remarks>
        /// The <see cref="AddCheckPlus{T}"/> cannot be set to a period value lower than 1 second.
        /// </remarks>
        /// <returns>The <see cref="IHealthChecksBuilder"/>.</returns>
        public static IHealthChecksBuilder AddCheckPlus<T>(this IHealthChecksBuilder ihb, string namedep, TimeSpan? delay = null, TimeSpan? period = null, IEnumerable<string>? tags = null, HealthStatus? failureStatus = null, TimeSpan? timeout = null) where T : IHealthCheck
#pragma warning restore CS0419 // Ambiguous reference in cref attribute
        {
            ArgumentNullException.ThrowIfNull(ihb);

            if (_enumType is null)
            {
                throw new InvalidOperationException("Invalid command. The HealthChecks enum must first be declared by the AddHealthChecksPlus command");
            }
            if (period.HasValue && period < TimeSpan.FromSeconds(1))
            {
                throw new ArgumentException($"The {nameof(period)} must be greater than or equal to one second.", nameof(period));
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
            ihb.Services.AddSingleton<IHealthCheckPlusPolicyStatus>(
                new HealthCheckPlusPolicyStatus(HealthStatus.Healthy, reg.Delay, reg.Period, namedep));

            return ihb;
        }

        
#pragma warning disable CS0419 // Ambiguous reference in cref attribute

        /// <summary>
        /// Register then dependence health check to run.
        /// </summary>
        /// <param name="ihb">The <see cref="IHealthChecksBuilder"/>.</param>
        /// <param name="enumdep">The enum value with health check list to run.</param>
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
        /// <remarks>
        /// The <see cref="AddCheckPlus{T}"/> cannot be set to a period value lower than 1 second.
        /// </remarks>
        /// <returns>The <see cref="IHealthChecksBuilder"/>.</returns>
        public static IHealthChecksBuilder AddCheckPlus<T>(this IHealthChecksBuilder ihb, Enum enumdep, TimeSpan? delay = null, TimeSpan? period = null, IEnumerable<string>? tags = null, HealthStatus? failureStatus = null, TimeSpan? timeout = null) where T : IHealthCheck
#pragma warning restore CS0419 // Ambiguous reference in cref attribute
        {
            ArgumentNullException.ThrowIfNull(ihb);

            if (_enumType is null)
            {
                throw new InvalidOperationException("Invalid command. The HealthChecks enum must first be declared by the AddHealthChecksPlus command");
            }
            if (!enumdep.GetType().Equals(_enumType))
            {
                throw new ArgumentException($"The {nameof(enumdep)} must be of type {_enumType.Name}", nameof(enumdep));
            }
            if (period.HasValue && period < TimeSpan.FromSeconds(1))
            {
                throw new ArgumentException($"The {nameof(period)} must be greater than or equal to one second.", nameof(period));
            }

            HealthCheckRegistration reg = new(
                    enumdep.ToString(),
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
            ihb.Services.AddSingleton<IHealthCheckPlusPolicyStatus>(
                new HealthCheckPlusPolicyStatus(HealthStatus.Healthy, reg.Delay, reg.Period, enumdep.ToString()));

            return ihb;
        }


#pragma warning disable CS0419 // Ambiguous reference in cref attribute
        /// <summary>
        /// Register then external(package import) dependence health check to run. the health check must added in <see cref="IHealthChecksBuilder"/>.
        /// </summary>
        /// <param name="ihb">The <see cref="IHealthChecksBuilder"/>.</param>
        /// <param name="enumdep">The enum value with health check list to run.</param>
        /// <param name="name">The name health check registered. This param is case insensitive</param>
        /// <param name="delay">An optional <see cref="TimeSpan"/>. The initial delay applied after the application starts before executing
        /// <see cref="IHealthCheckPublisher"/> instances. The delay is applied once at startup, and does
        /// not apply to subsequent iterations. The default value is 5 seconds.</param>
        /// <param name="period">An optional <see cref="TimeSpan"/>. The period of <see cref="IHealthCheckPublisher"/> execution. The default value is 30 seconds</param>
        /// <remarks>
        /// The <see cref="AddCheckLinkTo"/> cannot be set to a period value lower than 1 second.
        /// </remarks>
        /// <returns>The <see cref="IHealthChecksBuilder"/>.</returns>
        public static IHealthChecksBuilder AddCheckLinkTo(this IHealthChecksBuilder ihb, Enum enumdep, string name, TimeSpan? delay = null, TimeSpan? period = null)
#pragma warning restore CS0419 // Ambiguous reference in cref attribute
        {
            ArgumentNullException.ThrowIfNull(ihb);

            if (_enumType is null)
            {
                throw new InvalidOperationException("Invalid command. The HealthChecks enum must first be declared by the AddHealthChecksPlus command");
            }
            if (!enumdep.GetType().Equals(_enumType))
            {
                throw new ArgumentException($"The {nameof(enumdep)} must be of type {_enumType.Name}", nameof(enumdep));
            }
            if (period.HasValue && period < TimeSpan.FromSeconds(1))
            {
                throw new ArgumentException($"The {nameof(period)} must be greater than or equal to one second.", nameof(period));
            }

            if (enumdep.ToString().Equals(name, StringComparison.CurrentCultureIgnoreCase))
            {
                throw new ArgumentException($"Enum Name({enumdep}) has same name of registered check name({name}).");
            }

            ServiceDescriptor[] srvs_opt = ihb.Services
                .Where(x => x.ImplementationInstance != null && x.ServiceType.UnderlyingSystemType.FullName!.Contains("HealthCheckServiceOptions"))
                .ToArray();

            foreach (ServiceDescriptor? item in srvs_opt)
            {
                ConfigureNamedOptions<HealthCheckServiceOptions> hc_srvopt = (ConfigureNamedOptions<HealthCheckServiceOptions>)item.ImplementationInstance!;
                HealthCheckServiceOptions act_opt = new();
                hc_srvopt.Action!.Invoke(act_opt);
                if (act_opt.Registrations.Any(x => x.Name.Equals(name, StringComparison.CurrentCultureIgnoreCase)))
                {
                    //IHealthCheckPlusPolicyStatus? policy = null;

                    //save default register
                    ServiceDescriptor hcs = item;

                    HealthCheckRegistration reg = new(
                            enumdep.ToString(),
                            (sp) =>
                            {
                                if (_externalCheck.TryGetValue(enumdep.ToString(), out WrapperBaseHealthCheckPlus? value))
                                {
                                    return value;
                                }
                                //ensure existed in dict.
                                _externalCheck.TryAdd(enumdep.ToString(), new WrapperBaseHealthCheckPlus(act_opt.Registrations.First().Factory.Invoke(sp)));
                                return _externalCheck[enumdep.ToString()];
                            },
                            act_opt.Registrations.First().FailureStatus,
                            act_opt.Registrations.First().Tags,
                            act_opt.Registrations.First().Timeout)
                    {
                        Delay = delay,
                        Period = period
                    };
                    ihb.Add(reg);

                    //add policy for Healthy
                    ihb.Services.AddSingleton<IHealthCheckPlusPolicyStatus>(
                        new HealthCheckPlusPolicyStatus(HealthStatus.Healthy, reg.Delay, reg.Period, enumdep.ToString()));

                    //remove default register
                    if (hcs != null)
                    {
                        ihb.Services.Remove(hcs!);
                    }

                }
            }
            return ihb;
        }


#pragma warning disable CS0419 // Ambiguous reference in cref attribute
        /// <summary>
        /// Register then external(package import) dependence health check to run. the health check must added in <see cref="IHealthChecksBuilder"/>.
        /// </summary>
        /// <param name="ihb">The <see cref="IHealthChecksBuilder"/>.</param>
        /// <param name="namedep">The name health check list to run.</param>
        /// <param name="name">The name health check registered. This param is case insensitive</param>
        /// <param name="delay">An optional <see cref="TimeSpan"/>. The initial delay applied after the application starts before executing
        /// <see cref="IHealthCheckPublisher"/> instances. The delay is applied once at startup, and does
        /// not apply to subsequent iterations. The default value is 5 seconds.</param>
        /// <param name="period">An optional <see cref="TimeSpan"/>. The period of <see cref="IHealthCheckPublisher"/> execution. The default value is 30 seconds</param>
        /// <remarks>
        /// The <see cref="AddCheckLinkTo"/> cannot be set to a period value lower than 1 second.
        /// </remarks>
        /// <returns>The <see cref="IHealthChecksBuilder"/>.</returns>
        public static IHealthChecksBuilder AddCheckLinkTo(this IHealthChecksBuilder ihb, string namedep, string name, TimeSpan? delay = null, TimeSpan? period = null)
#pragma warning restore CS0419 // Ambiguous reference in cref attribute
        {
            ArgumentNullException.ThrowIfNull(ihb);

            if (_enumType is null)
            {
                throw new InvalidOperationException("Invalid command. The HealthChecks enum must first be declared by the AddHealthChecksPlus command");
            }
            if (period.HasValue && period < TimeSpan.FromSeconds(1))
            {
                throw new ArgumentException($"The {nameof(period)} must be greater than or equal to one second.", nameof(period));
            }

            if (namedep.Equals(name, StringComparison.CurrentCultureIgnoreCase))
            {
                throw new ArgumentException($"Enum Name({namedep}) has same name of registered check name({name}).");
            }

            ServiceDescriptor[] srvs_opt = ihb.Services
                .Where(x => x.ImplementationInstance != null && x.ServiceType.UnderlyingSystemType.FullName!.Contains("HealthCheckServiceOptions"))
                .ToArray();

            foreach (ServiceDescriptor? item in srvs_opt)
            {
                ConfigureNamedOptions<HealthCheckServiceOptions> hc_srvopt = (ConfigureNamedOptions<HealthCheckServiceOptions>)item.ImplementationInstance!;
                HealthCheckServiceOptions act_opt = new();
                hc_srvopt.Action!.Invoke(act_opt);
                if (act_opt.Registrations.Any(x => x.Name.Equals(name, StringComparison.CurrentCultureIgnoreCase)))
                {
                    //IHealthCheckPlusPolicyStatus? policy = null;

                    //save default register
                    ServiceDescriptor hcs = item;

                    HealthCheckRegistration reg = new(
                            namedep,
                            (sp) =>
                            {
                                if (_externalCheck.TryGetValue(namedep, out WrapperBaseHealthCheckPlus? value))
                                {
                                    return value;
                                }
                                //ensure existed in dict.
                                _externalCheck.TryAdd(namedep, new WrapperBaseHealthCheckPlus(act_opt.Registrations.First().Factory.Invoke(sp)));
                                return _externalCheck[namedep];
                            },
                            act_opt.Registrations.First().FailureStatus,
                            act_opt.Registrations.First().Tags,
                            act_opt.Registrations.First().Timeout)
                    {
                        Delay = delay,
                        Period = period
                    };
                    ihb.Add(reg);

                    //add policy for Healthy
                    ihb.Services.AddSingleton<IHealthCheckPlusPolicyStatus>(
                        new HealthCheckPlusPolicyStatus(HealthStatus.Healthy, reg.Delay, reg.Period, namedep));

                    //remove default register
                    if (hcs != null)
                    {
                        ihb.Services.Remove(hcs!);
                    }

                }
            }
            return ihb;
        }
    }
}
