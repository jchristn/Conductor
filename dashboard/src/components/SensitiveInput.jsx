import React, { useState } from 'react';

function SensitiveInput({ id, value, onChange, placeholder, required = false, autoComplete = 'off' }) {
  const [visible, setVisible] = useState(false);

  return (
    <div className="sensitive-input">
      <input
        type={visible ? 'text' : 'password'}
        id={id}
        value={value}
        onChange={onChange}
        placeholder={placeholder}
        required={required}
        autoComplete={autoComplete}
      />
      <button
        type="button"
        className="sensitive-input-toggle"
        onClick={() => setVisible((current) => !current)}
        title={visible ? 'Hide value' : 'Show value'}
        aria-label={visible ? 'Hide value' : 'Show value'}
      >
        {visible ? (
          <svg width="16" height="16" viewBox="0 0 20 20" fill="currentColor">
            <path d="M3.707 2.293a1 1 0 00-1.414 1.414l1.848 1.848A9.772 9.772 0 001 10c1.5 3.5 5.09 6 9 6a8.95 8.95 0 004.445-1.174l1.848 1.848a1 1 0 001.414-1.414l-14-14zM12.96 13.374A6.958 6.958 0 0110 14c-3.002 0-5.836-1.716-7.287-4A7.788 7.788 0 015.55 6.53l1.524 1.524A4 4 0 0010 14a3.98 3.98 0 002.396-.626l.564.564zM10 6a3.98 3.98 0 012.396.626l2.023 2.023A7.782 7.782 0 0117.287 10a7.788 7.788 0 01-1.615 2.12l1.43 1.43A9.76 9.76 0 0019 10c-1.5-3.5-5.09-6-9-6-1.092 0-2.14.195-3.111.555L8.53 6.197A3.98 3.98 0 0110 6zm0 2a2 2 0 011.995 1.85l-2.145-2.145c.05-.003.1-.005.15-.005z" />
          </svg>
        ) : (
          <svg width="16" height="16" viewBox="0 0 20 20" fill="currentColor">
            <path d="M10 4c3.91 0 7.5 2.5 9 6-1.5 3.5-5.09 6-9 6s-7.5-2.5-9-6c1.5-3.5 5.09-6 9-6zm0 2C7.002 6 4.164 7.716 2.713 10 4.164 12.284 7.002 14 10 14s5.836-1.716 7.287-4C15.836 7.716 12.998 6 10 6zm0 1.5A2.5 2.5 0 1110 12.5 2.5 2.5 0 0110 7.5z" />
          </svg>
        )}
      </button>
    </div>
  );
}

export default SensitiveInput;
