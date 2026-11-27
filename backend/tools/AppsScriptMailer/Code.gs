// Mail relay for the API. Deploy as a web app that executes as the sending account.
// Set the MAIL_SECRET script property to the same value as Email__ScriptSecret.

const MINIMUM_SECRET_LENGTH = 32;

function doPost(event) {
  const mail = readRequest(event);
  const secret = PropertiesService.getScriptProperties().getProperty('MAIL_SECRET');

  if (!secret || secret.length < MINIMUM_SECRET_LENGTH || !mail || mail.secret !== secret) {
    return reply('rejected');
  }

  // One recipient per request keeps the daily quota predictable.
  if (typeof mail.recipient !== 'string' || mail.recipient.includes(',')) {
    return reply('rejected');
  }

  try {
    const message = {
      to: mail.recipient,
      subject: mail.subject,
      body: mail.body,
      name: mail.senderName,
    };

    if (mail.htmlBody) {
      message.htmlBody = mail.htmlBody;
    }

    MailApp.sendEmail(message);
    return reply('sent');
  } catch (error) {
    // The error name only: messages can contain recipient addresses.
    console.error(error.name);
    return reply('failed');
  }
}

// Run once from the editor to grant the send permission before deploying.
function authorize() {
  console.log('Remaining daily recipients: ' + MailApp.getRemainingDailyQuota());
}

function readRequest(event) {
  try {
    return JSON.parse(event.postData.contents);
  } catch (error) {
    return null;
  }
}

function reply(status) {
  return ContentService.createTextOutput(JSON.stringify({ status: status })).setMimeType(ContentService.MimeType.JSON);
}
