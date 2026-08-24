// This file previously contained a placeholder test (MfaBypassTests_AreMarkedForHumanImplementation)
// that always passed and served only as a CI reminder.
//
// The placeholder has been removed and replaced with real integration tests in:
//   HRMS.Tests/Security/MfaIntegrationTests.cs
//
// That file contains three test suites:
//   A. MfaHappyPathTests     — full MFA login flow (service-level)
//   B. MfaBypassHttpTests    — temp token cannot access protected endpoints (HTTP-level)
//   C. MfaRefreshTokenTests  — refresh token MFA-verification enforcement (service-level)
namespace HRMS.Tests.Security;
