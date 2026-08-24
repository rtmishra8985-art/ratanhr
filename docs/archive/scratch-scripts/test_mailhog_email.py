#!/usr/bin/env python3
"""
RatanHR Phase 8 - MailHog SMTP Test Email Script
Tests email delivery to local MailHog server
"""

import smtplib
from email.mime.text import MIMEText
from email.mime.multipart import MIMEMultipart
from datetime import datetime

# MailHog Configuration
MAILHOG_HOST = "localhost"
MAILHOG_PORT = 1025
FROM_EMAIL = "rtmishra8985@gmail.com"
TO_EMAIL = "rtmishra7040@gmail.com"

print("=" * 80)
print("RatanHR Phase 8 - MailHog SMTP Test Email")
print("=" * 80)
print()

try:
    print(f"[1/3] Connecting to MailHog: {MAILHOG_HOST}:{MAILHOG_PORT}")
    server = smtplib.SMTP(MAILHOG_HOST, MAILHOG_PORT, timeout=10)
    server.set_debuglevel(1)
    print("✓ Connected to MailHog SMTP server")
    print()
    
    print("[2/3] Creating email message...")
    msg = MIMEMultipart("alternative")
    msg["Subject"] = "🟢 RatanHR Phase 8 - MailHog TEST EMAIL"
    msg["From"] = FROM_EMAIL
    msg["To"] = TO_EMAIL
    
    # HTML email body
    html = f"""
    <html>
      <body>
        <h1>🟢 RatanHR Phase 8 - MailHog Test Email</h1>
        
        <h2>Email Configuration Test</h2>
        <p><strong>Status:</strong> ✅ MAILHOG CONFIGURED & WORKING</p>
        
        <h3>Test Details</h3>
        <ul>
          <li><strong>From:</strong> {FROM_EMAIL}</li>
          <li><strong>To:</strong> {TO_EMAIL}</li>
          <li><strong>Time:</strong> {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}</li>
          <li><strong>Server:</strong> MailHog (localhost:1025)</li>
          <li><strong>Protocol:</strong> SMTP (no authentication)</li>
        </ul>
        
        <h3>What This Means</h3>
        <p>✅ Email service is configured for local testing</p>
        <p>✅ MailHog is capturing all emails sent</p>
        <p>✅ Ready for on-premise deployment</p>
        
        <h3>Next Steps</h3>
        <ol>
          <li>Check MailHog Web UI: <a href="http://localhost:8025">http://localhost:8025</a></li>
          <li>This email should be visible in the inbox</li>
          <li>When ready for production, switch to Brevo SMTP</li>
        </ol>
        
        <hr>
        <p><em>This is a test email from RatanHR Phase 8 SMTP Configuration</em></p>
      </body>
    </html>
    """
    
    msg.attach(MIMEText(html, "html"))
    print("✓ Email message created")
    print()
    
    print("[3/3] Sending email via MailHog SMTP...")
    server.sendmail(FROM_EMAIL, TO_EMAIL, msg.as_string())
    server.quit()
    print("✓ Email sent successfully!")
    print()
    
    print("=" * 80)
    print("✅ SUCCESS - EMAIL SENT TO MAILHOG")
    print("=" * 80)
    print()
    print("Next: Open http://localhost:8025 to view the email in MailHog inbox")
    print()
    
except smtplib.SMTPException as e:
    print(f"❌ SMTP Error: {e}")
    print()
except Exception as e:
    print(f"❌ Error: {e}")
    print()
    print("Troubleshooting:")
    print("  1. Is MailHog running? (Check command window)")
    print("  2. Is port 1025 available?")
    print("  3. Try: python -c 'import socket; s = socket.create_connection((\"localhost\", 1025))'")
    print()
