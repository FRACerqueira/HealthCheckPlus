# **Welcome to HealthCheckPlus**

### **HealthCheck with individual delay and interval and interval policy for unhealthy/degraded status.**

**HealthCheckPlus** was developed in c# with the **.Net6**, **.Net7** and **.Net8** target frameworks.

## What's new in the latest version 

### V2.0.0
[**Top**](#table-of-contents)

- Complete refactoring to only extend native functionalities without changing their behavior when the new features are not used.

## Features
[**Top**](#table-of-contents)

- Command to Change to unhealthy/degraded any HealthCheck by forcing check by interval policy
- Command to retrieve the last result of each HealthCheck kept in cache
- Optional Delay and interval for each HealthCheck 
    - Policy for Healthy while keeping results cached (default)
    - Policy for degraded (Optional)
    - Policy for unhealthy (Optional)
- Register an external health check (package import) and associate delay, interval and individual policy rules.
- Policy background service for updating and running HealthChecks
    - Optional set delay and interval are used in the background update service parameters when defined and HealthCheck is null for delay and interval
    - Integration with registered publishers with the interface IHealthCheckPublisher with extra filters:
        - Number of counts idle to publish.
        - Run publish only when the report has a status change in one of its entries.
- Response templates with small/full details in "application/json" ContentType
    - HealthCheckPlusOptions.WriteShortDetails
    - HealthCheckPlusOptions.WriteShortDetailsPlus (with extra fields : cache source and reference date of last run)
    - HealthCheckPlusOptions.WriteDetailsWithoutException
    - HealthCheckPlusOptions.WriteDetailsWithoutExceptionPlus (with extra fields : cache source and reference date of last run)
    - HealthCheckPlusOptions.WriteDetailsWithException
    - HealthCheckPlusOptions.WriteDetailsWithExceptionPlus (with extra fields : cache source and reference date of last run)
- Simple and clear fluent syntax extending the native features of healt check


## License

Copyright 2023 @ Fernando Cerqueira

HealthCheckPlus is licensed under the MIT license. See [LICENSE](https://github.com/FRACerqueira/HealthCheckPlus/blob/master/LICENSE).

