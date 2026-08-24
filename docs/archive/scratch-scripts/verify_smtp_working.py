#!/usr/bin/env python3
"""
RatanHR Phase 8 - Simple SMTP Connection Test
Verify if Brevo SMTP is reachable and working
"""

import smtplib
import sys

# Brevo SMTP Configuration
SMTP_HOST = "smtp-relay.brevo.com"
SMTP_PORT = 587
SMTP_USERNAME = "b5ef15001@smtp-brevo.com"
SMTP_PASSWORD = "xkeysib-e8fc2e1e5e8c1c8c9d7f8e9c0d1e2f3g4h5i6j7k8l9m0n1o2p3q4r5s6t7"

print()
print("═" * 60)
print("RatanHR Phase 8 - SMTP Connection Verification")
print("═" * 60)
print()

# Test 1: DNS Resolution
print("[TEST 1] Checking if SMTP server is reachable...")
try:
    import socket
    socket.gethostbyname(SMTP_HOST)
    print(f"  ✓ DNS Resolution: {SMTP_HOST} - RESOLVED")
except socket.gaierror:
    print(f"  ✗ DNS Resolution: {SMTP_HOST} - FAILED")
    print("    Cannot resolve host. Check network connectivity.")
    sys.exit(1)

print()

# Test 2: Port Connection
print("[TEST 2] Checking SMTP port connectivity...")
try:
    sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    sock.settimeout(5)
    result = sock.connect_ex((SMTP_HOST, SMTP_PORT))
    sock.close()
    
    if result == 0:
        print(f"  ✓ Port {SMTP_PORT}: OPEN & REACHABLE")
    else:
        print(f"  ✗ Port {SMTP_PORT}: BLOCKED or UNREACHABLE")
        print("    Check firewall settings. Port 587 may be blocked.")
        sys.exit(1)
except Exception as e:
    print(f"  ✗ Error: {e}")
    sys.exit(1)

print()

# Test 3: SMTP Connection
print("[TEST 3] Connecting to SMTP server...")
try:
    server = smtplib.SMTP(SMTP_HOST, SMTP_PORT, timeout=10)
    print(f"  ✓ Connection: SUCCESS")
    print(f"    Server Response: {server.helo()[0]}")
except Exception as e:
    print(f"  ✗ Connection: FAILED")
    print(f"    Error: {e}")
    sys.exit(1)

print()

# Test 4: STARTTLS
print("[TEST 4] Enabling TLS/STARTTLS...")
try:
    server.starttls()
    print(f"  ✓ TLS: ENABLED")
except Exception as e:
    print(f"  ✗ TLS: FAILED")
    print(f"    Error: {e}")
    server.quit()
    sys.exit(1)

print()

# Test 5: Authentication
print("[TEST 5] Authenticating with Brevo...")
try:
    server.login(SMTP_USERNAME, SMTP_PASSWORD)
    print(f"  ✓ Authentication: SUCCESS")
    print(f"    Username: {SMTP_USERNAME}")
except smtplib.SMTPAuthenticationError as e:
    print(f"  ✗ Authentication: FAILED")
    print(f"    Error: Invalid username or password")
    print(f"    Check Brevo SMTP key at: https://app.brevo.com/settings/keys/smtp")
    server.quit()
    sys.exit(1)
except Exception as e:
    print(f"  ✗ Authentication: ERROR")
    print(f"    Error: {e}")
    server.quit()
    sys.exit(1)

print()

# Test 6: Send Test Email
print("[TEST 6] Sending test email...")
try:
    from email.mime.text import MIMEText
    from email.mime.multipart import MIMEMultipart
    from datetime import datetime
    
    TO_EMAIL = "rtmishra7040@gmail.com"
    FROM_EMAIL = "rtmishra8985@gmail.com"
    FROM_NAME = "RatanHR HRMS"
    SUBJECT = "RatanHR Phase 8 Test - SMTP Verification"
    
    timestamp = datetime.utcnow().strftime("%Y-%m-%d %H:%M:%S")
    
    msg = MIMEMultipart("alternative")
    msg["Subject"] = SUBJECT
    msg["From"] = f"{FROM_NAME} <{FROM_EMAIL}>"
    msg["To"] = TO_EMAIL
    
    html_body = f"""
    <html>
    <body style="font-family: Arial; background: #f5f5f5;">
        <div style="background: white; padding: 30px; max-width: 600px; margin: 20px auto; border-radius: 8px;">
            <h2 style="color: #4CAF50;">✅ SMTP Email Test Successful</h2>
            <p>This email confirms that Brevo SMTP is working correctly.</p>
            <table style="width: 100%; border-collapse: collapse;">
                <tr>
                    <td style="padding: 10px; background: #f0f0f0; border: 1px solid #ddd;"><b>Status</b></td>
                    <td style="padding: 10px; border: 1px solid #ddd;"><span style="color: #4CAF50;"><b>✓ WORKING</b></span></td>
                </tr>
                <tr>
                    <td style="padding: 10px; background: #f0f0f0; border: 1px solid #ddd;"><b>Sent At</b></td>
                    <td style="padding: 10px; border: 1px solid #ddd;">{timestamp} UTC</td>
                </tr>
                <tr>
                    <td style="padding: 10px; background: #f0f0f0; border: 1px solid #ddd;"><b>From</b></td>
                    <td style="padding: 10px; border: 1px solid #ddd;">{FROM_EMAIL}</td>
                </tr>
                <tr>
                    <td style="padding: 10px; background: #f0f0f0; border: 1px solid #ddd;"><b>To</b></td>
                    <td style="padding: 10px; border: 1px solid #ddd;">{TO_EMAIL}</td>
                </tr>
                <tr>
                    <td style="padding: 10px; background: #f0f0f0; border: 1px solid #ddd;"><b>Server</b></td>
                    <td style="padding: 10px; border: 1px solid #ddd;">smtp-relay.brevo.com:587</td>
                </tr>
            </table>
            <p style="margin-top: 20px; color: #666; font-size: 12px;">
                RatanHR HRMS v1.0.4 | Phase 8 SMTP Test | {timestamp}
            </p>
        </div>
    </body>
    </html>
    """
    
    part = MIMEText(html_body, "html")
    msg.attach(part)
    
    server.sendmail(FROM_EMAIL, TO_EMAIL, msg.as_string())
    print(f"  ✓ Email: SENT")
    print(f"    To: {TO_EMAIL}")
    
except Exception as e:
    print(f"  ✗ Email Send: FAILED")
    print(f"    Error: {e}")
    server.quit()
    sys.exit(1)

print()

# Cleanup
server.quit()

print("═" * 60)
print("✅ ALL TESTS PASSED - SMTP IS WORKING!")
print("═" * 60)
print()
print("[SUMMARY]")
print("  ✓ DNS Resolution: PASSED")
print("  ✓ Port Connectivity: PASSED")
print("  ✓ SMTP Connection: PASSED")
print("  ✓ TLS/STARTTLS: PASSED")
print("  ✓ Authentication: PASSED")
print("  ✓ Email Delivery: PASSED")
print()
print("[RESULT]")
print(f"  Status: ✅ SMTP EMAIL SYSTEM IS FULLY OPERATIONAL")
print(f"  Test Email Sent To: rtmishra7040@gmail.com")
print(f"  Expected Delivery: 1-2 minutes")
print()
print("[NEXT STEPS]")
print("  1. Check email inbox: rtmishra7040@gmail.com")
print("  2. Wait 1-2 minutes for delivery")
print("  3. Verify email from: rtmishra8985@gmail.com")
print("  4. Confirm Phase 8 verification details")
print()
