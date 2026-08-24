global using Xunit;
global using Moq;
global using FluentAssertions;
global using Microsoft.EntityFrameworkCore;

// Entity namespaces — bring concrete entity types into scope for all test files
global using HRMS.Domain.Entities.Employee;
global using HRMS.Domain.Entities.Authentication;
global using HRMS.Domain.Entities.Attendance;
global using HRMS.Domain.Entities.Payroll;
global using HRMS.Domain.Entities.Leave;
global using HRMS.Domain.Entities.Sales;
global using HRMS.Domain.Entities.Timesheet;
global using HRMS.Domain.Entities.Webhook;
global using HRMS.Domain.Entities.Company;

// Application DTO namespaces
global using HRMS.Application.DTOs.Auth;
global using HRMS.Application.DTOs.Company;
global using HRMS.Application.DTOs.Employee;
global using HRMS.Application.DTOs.Timesheet;
global using HRMS.Application.DTOs.Sales;
global using HRMS.Application.DTOs.Webhook;
global using HRMS.Application.DTOs.Report;

// Infrastructure namespaces
global using HRMS.Infrastructure.BackgroundServices;
global using HRMS.Infrastructure.Services;
