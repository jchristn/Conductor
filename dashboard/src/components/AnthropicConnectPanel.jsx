import React from 'react';
import CopyButton from './CopyButton';

/**
 * "Connect a client" instructions for an Anthropic VMR: the environment variables Claude Code
 * and the Anthropic SDKs read, with copy controls.
 */
function AnthropicConnectPanel({ baseUrl }) {
  const tokenPlaceholder = '<your Conductor credential bearer token>';
  const exportBlock = `export ANTHROPIC_BASE_URL="${baseUrl}"\nexport ANTHROPIC_AUTH_TOKEN="${tokenPlaceholder}"`;

  return (
    <section className="detail-section connect-client-panel" aria-label="Connect a client">
      <h3>Connect a client</h3>
      <p className="form-help">
        Point Claude Code or an Anthropic SDK at this VMR with these environment variables. The SDKs append
        {' '}<code>/v1/messages</code> to the base URL.
      </p>
      <div className="detail-grid detail-grid-two">
        <div className="detail-item">
          <label>ANTHROPIC_BASE_URL</label>
          <div className="token-cell">
            <code>{baseUrl}</code>
            <CopyButton value={baseUrl} title="Copy ANTHROPIC_BASE_URL" />
          </div>
        </div>
        <div className="detail-item">
          <label>ANTHROPIC_AUTH_TOKEN</label>
          <div className="token-cell">
            <code>{tokenPlaceholder}</code>
            <CopyButton value="ANTHROPIC_AUTH_TOKEN" title="Copy ANTHROPIC_AUTH_TOKEN variable name" />
          </div>
        </div>
      </div>
      <div className="code-block connect-client-snippet">
        <pre>{exportBlock}</pre>
        <CopyButton value={exportBlock} title="Copy environment variables" />
      </div>
      <p className="form-help">
        ANTHROPIC_AUTH_TOKEN is a Conductor credential (a bearer token from Credentials), not an Anthropic key. Clients send it
        as <code>Authorization: Bearer</code>. On Anthropic VMRs Conductor also accepts the credential in <code>x-api-key</code>,
        so <code>ANTHROPIC_API_KEY</code> works too. The credential is never forwarded upstream: Conductor calls Anthropic with
        the endpoint&apos;s own key.
      </p>
    </section>
  );
}

export default AnthropicConnectPanel;
