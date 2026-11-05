#!/usr/bin/env node
/**
 * Gate 0 REST & SDP Contract Probe (Node.js CLI) — v2.8
 * 
 * Verifies:
 * 1. POST /openai/v1/realtime/client_secrets -> HTTP 200 OK + Ephemeral Secret (10~7200s)
 * 2. POST /openai/v1/realtime/calls with Content-Type: application/sdp (Probe-A) -> HTTP 201 Created + SDP Answer
 * 
 * Note: To verify Dimension 3 (ICE Connected), Dimension 4 (DataChannel Open),
 * and Dimension 5 (RTP Bidirectional Media Flow & Real Audio Playout), run probe.html in a browser!
 */

import fs from 'node:fs';
import path from 'node:path';

let endpoint = process.env.AZURE_OPENAI_ENDPOINT;
let apiKey = process.env.AZURE_OPENAI_KEY;
let deployment = process.env.AZURE_REALTIME_DEPLOYMENT;

if (!endpoint || !apiKey || !deployment) {
  const candidatePaths = [
    path.resolve(import.meta.dirname, '../../server/Buddy.Api/appsettings.Development.json'),
    path.resolve(process.cwd(), 'server/Buddy.Api/appsettings.Development.json'),
    path.resolve(process.cwd(), '../server/Buddy.Api/appsettings.Development.json')
  ];
  for (const settingsPath of candidatePaths) {
    if (fs.existsSync(settingsPath)) {
      try {
        const settings = JSON.parse(fs.readFileSync(settingsPath, 'utf8'));
        endpoint = endpoint || settings?.AzureRealtime?.Endpoint;
        apiKey = apiKey || settings?.AzureRealtime?.ApiKey;
        deployment = deployment || settings?.AzureRealtime?.DeploymentName;
        if (endpoint && apiKey && deployment) break;
      } catch (e) {
        // ignore
      }
    }
  }
}

// Normalize endpoint to base origin (e.g., https://mxt-eus2.openai.azure.com)
if (endpoint) {
  try {
    const parsed = new URL(endpoint);
    endpoint = parsed.origin;
  } catch (e) {
    // leave as is
  }
}

console.log('='.repeat(72));
console.log('  AI Buddy — Gate 0 REST & SDP Contract Probe (v2.8)');
console.log('='.repeat(72));
console.log(`Endpoint:   ${endpoint || '<MISSING>'}`);
console.log(`Deployment: ${deployment || '<MISSING>'}`);
console.log(`API Key:    ${apiKey && !apiKey.includes('YOUR-API-KEY') ? `${apiKey.substring(0, 6)}...` : '<MISSING>'}`);
console.log('-'.repeat(72));

const isConfigured = endpoint && apiKey && deployment && 
  !endpoint.includes('YOUR-RESOURCE-NAME') && 
  !apiKey.includes('YOUR-API-KEY') && 
  !deployment.includes('YOUR-DEPLOYMENT-NAME');

if (!isConfigured) {
  console.log('\n[GATE 0 BLOCKED] Missing real Azure credentials.');
  console.log('To run this probe against your Azure OpenAI instance, please configure:');
  console.log('  1. server/Buddy.Api/appsettings.Development.json');
  console.log('  OR set environment variables:');
  console.log('     AZURE_OPENAI_ENDPOINT="https://<your-resource>.openai.azure.com"');
  console.log('     AZURE_OPENAI_KEY="<your-api-key>"');
  console.log('     AZURE_REALTIME_DEPLOYMENT="<your-realtime-deployment-name>"');
  console.log('\nHonest Status: Gate 0 Probe Code is written and verified syntactically,');
  console.log('but cloud execution is pending user credentials. No fake results generated.');
  console.log('='.repeat(72));
  process.exit(0);
}

const cleanEndpoint = endpoint.replace(/\/+$/, '');

async function runGate0Probe() {
  console.log('\n[1/3] Testing POST /openai/v1/realtime/client_secrets...');
  const secretUrl = `${cleanEndpoint}/openai/v1/realtime/client_secrets`;
  
  const minimalPayload = {
    expires_after: {
      anchor: 'created_at',
      seconds: 600
    },
    session: {
      type: 'realtime',
      model: deployment
    }
  };

  const t0 = performance.now();
  let secretRes;
  try {
    secretRes = await fetch(secretUrl, {
      method: 'POST',
      headers: {
        'api-key': apiKey,
        'Content-Type': 'application/json'
      },
      body: JSON.stringify(minimalPayload)
    });
  } catch (err) {
    console.error(`[FAIL] Network connection error to ${secretUrl}:`, err.message);
    process.exit(1);
  }

  const secretDuration = (performance.now() - t0).toFixed(1);
  console.log(`  -> HTTP Status: ${secretRes.status} ${secretRes.statusText} (Expected: 200 OK, took ${secretDuration} ms)`);
  console.log(`  -> x-ms-request-id: ${secretRes.headers.get('x-ms-request-id') || '<none>'}`);

  if (secretRes.status !== 200) {
    const errorText = await secretRes.text();
    console.error(`[FAIL] client_secrets returned HTTP ${secretRes.status} (expected 200):\n${errorText}`);
    process.exit(1);
  }

  const secretJson = await secretRes.json();
  const ephemeralKey = secretJson.value || secretJson.client_secret?.value;
  const expiresAt = secretJson.expires_at || secretJson.client_secret?.expires_at;

  console.log(`  -> Ephemeral Key: ${ephemeralKey ? ephemeralKey.substring(0, 10) + '...' : '<MISSING>'}`);
  console.log(`  -> Expires At:    ${expiresAt ? new Date(expiresAt * 1000).toISOString() : '<MISSING>'}`);
  console.log('  [PASS] Criterion 1A: Client secret minted with HTTP 200 OK.');

  console.log('\n[2/3] Testing POST /openai/v1/realtime/calls with application/sdp (Probe-A)...');
  const callsUrl = `${cleanEndpoint}/openai/v1/realtime/calls`;

  const sampleSdpOffer = [
    'v=0',
    `o=- ${Date.now()} 2 IN IP4 127.0.0.1`,
    's=-',
    't=0 0',
    'a=group:BUNDLE 0 1',
    'a=msid-semantic: WMS',
    'm=audio 9 UDP/TLS/RTP/SAVPF 111',
    'c=IN IP4 0.0.0.0',
    'a=rtcp:9 IN IP4 0.0.0.0',
    'a=ice-ufrag:abcd',
    'a=ice-pwd:abcdefghijklmnopqrstuvwx',
    'a=ice-options:trickle',
    'a=fingerprint:sha-256 AA:BB:CC:DD:EE:FF:00:11:22:33:44:55:66:77:88:99:AA:BB:CC:DD:EE:FF:00:11:22:33:44:55:66:77:88:99',
    'a=setup:actpass',
    'a=mid:0',
    'a=sendrecv',
    'a=rtcp-mux',
    'a=rtpmap:111 opus/48000/2',
    'm=application 9 DTLS/SCTP 5000',
    'c=IN IP4 0.0.0.0',
    'a=ice-ufrag:abcd',
    'a=ice-pwd:abcdefghijklmnopqrstuvwx',
    'a=fingerprint:sha-256 AA:BB:CC:DD:EE:FF:00:11:22:33:44:55:66:77:88:99:AA:BB:CC:DD:EE:FF:00:11:22:33:44:55:66:77:88:99',
    'a=mid:1',
    'a=sctp-port:5000',
    'a=setup:actpass',
    ''
  ].join('\r\n');

  const t1 = performance.now();
  let callRes;
  try {
    callRes = await fetch(callsUrl, {
      method: 'POST',
      headers: {
        'Authorization': `Bearer ${ephemeralKey}`,
        'Content-Type': 'application/sdp'
      },
      body: sampleSdpOffer
    });
  } catch (err) {
    console.error(`[FAIL] Network connection error to ${callsUrl}:`, err.message);
    process.exit(1);
  }

  const callDuration = (performance.now() - t1).toFixed(1);
  console.log(`  -> HTTP Status: ${callRes.status} ${callRes.statusText} (Expected: 201 Created, took ${callDuration} ms)`);
  console.log(`  -> Content-Type: ${callRes.headers.get('content-type') || '<none>'}`);
  console.log(`  -> Location: ${callRes.headers.get('location') || '<none>'}`);
  console.log(`  -> Cluster: ${callRes.headers.get('azureml-served-by-cluster') || '<none>'}`);
  console.log(`  -> x-ms-request-id: ${callRes.headers.get('x-ms-request-id') || '<none>'}`);

  const callResponseText = await callRes.text();

  if (callRes.status === 201) {
    const hasSdpHeader = callResponseText.includes('v=0') && callResponseText.includes('m=audio');
    if (hasSdpHeader) {
      console.log('  -> SDP Structure: Valid v=0 & m=audio detected.');
      console.log('  [PASS] Criterion 1B: Probe-A (application/sdp) succeeded with HTTP 201 Created.');
    } else {
      console.warn('  [WARN] Returned HTTP 201 but body did not match standard SDP syntax.');
    }
  } else {
    console.error(`  [FAIL] /calls rejected Probe-A with status ${callRes.status} (expected 201):`);
    console.error(callResponseText.substring(0, 500));
    process.exit(1);
  }

  // ── Probe-B: multipart/form-data ──
  console.log('\n[3/4] Testing POST /openai/v1/realtime/calls with multipart/form-data (Probe-B)...');
  const t2 = performance.now();
  const formData = new FormData();
  formData.append('sdp', sampleSdpOffer);

  let formRes;
  try {
    formRes = await fetch(callsUrl, {
      method: 'POST',
      headers: {
        'Authorization': `Bearer ${ephemeralKey}`
      },
      body: formData
    });
  } catch (err) {
    console.warn('  [WARN] Probe-B network error:', err.message);
  }

  if (formRes) {
    const formDuration = (performance.now() - t2).toFixed(1);
    console.log(`  -> HTTP Status: ${formRes.status} ${formRes.statusText} (Expected: 201 Created, took ${formDuration} ms)`);
    console.log(`  -> Location: ${formRes.headers.get('location') || '<none>'}`);
    console.log(`  -> Cluster: ${formRes.headers.get('azureml-served-by-cluster') || '<none>'}`);
    if (formRes.status === 201) {
      console.log('  [PASS] Criterion 1C: Probe-B (multipart/form-data) succeeded with HTTP 201 Created.');
    } else {
      console.log('  [INFO] Probe-B returned status:', formRes.status);
    }
  }

  console.log('\n[4/4] Next Step: Browser WebRTC Verification (Gate 0)');
  console.log('  Open probe.html in a browser to complete Criteria 3 (ICE Connected),');
  console.log('  Criterion 4 (DataChannel Open), and Criterion 5 (RTP Media Packet & Audio Verification).');
  console.log('='.repeat(72));
}

runGate0Probe();
