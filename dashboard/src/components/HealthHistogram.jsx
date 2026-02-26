import React from 'react';

function HealthHistogram({ history, width = 120, height = 24, fill = false, onClick }) {
  if (!history || history.length === 0) {
    return <span style={{ color: 'var(--text-secondary)', fontSize: '12px' }}>No data</span>;
  }

  const now = new Date();
  const sorted = [...history].sort((a, b) => new Date(a.TimestampUtc) - new Date(b.TimestampUtc));
  const oldest = new Date(sorted[0].TimestampUtc);
  const spanMs = now - oldest;
  const spanHours = spanMs / (1000 * 60 * 60);

  let buckets = [];
  if (spanHours < 1) {
    buckets = sorted.map(r => ({
      success: r.Success ? 1 : 0,
      fail: r.Success ? 0 : 1,
      time: r.TimestampUtc
    }));
  } else {
    const bucketMs = spanHours <= 6 ? 60000 : 300000;
    const bucketMap = new Map();
    for (const r of sorted) {
      const t = new Date(r.TimestampUtc).getTime();
      const key = Math.floor(t / bucketMs);
      if (!bucketMap.has(key)) bucketMap.set(key, { success: 0, fail: 0 });
      const b = bucketMap.get(key);
      if (r.Success) b.success++;
      else b.fail++;
    }
    for (const [key, val] of bucketMap) {
      buckets.push({ ...val, time: new Date(key * bucketMs).toISOString() });
    }
  }

  if (buckets.length === 0) {
    return <span style={{ color: 'var(--text-secondary)', fontSize: '12px' }}>No data</span>;
  }

  // Cap to most recent bars based on available width (min 6px per bar)
  const maxBars = Math.floor(width / 6);
  if (buckets.length > maxBars) {
    buckets = buckets.slice(-maxBars);
  }

  const barGap = 2;
  const barWidth = Math.max(4, Math.floor(width / buckets.length) - barGap);
  const totalWidth = buckets.length * (barWidth + barGap) - barGap;

  return (
    <svg
      viewBox={`0 0 ${totalWidth} ${height}`}
      width={fill ? '100%' : totalWidth}
      height={height}
      preserveAspectRatio="none"
      style={{ cursor: onClick ? 'pointer' : 'default', display: 'block' }}
      onClick={onClick}
      role={onClick ? 'button' : undefined}
      aria-label="Health check history"
    >
      {buckets.map((b, i) => {
        const total = b.success + b.fail;
        const allSuccess = b.fail === 0;
        const allFail = b.success === 0;
        const color = allSuccess ? '#10b981' : allFail ? '#ef4444' : '#f59e0b';
        const x = i * (barWidth + barGap);
        const timeStr = new Date(b.time).toLocaleTimeString();
        const titleText = `${timeStr}: ${b.success} ok, ${b.fail} fail (${total} total)`;

        return (
          <rect
            key={i}
            x={x}
            y={0}
            width={barWidth}
            height={height}
            rx={1}
            fill={color}
            opacity={0.85}
          >
            <title>{titleText}</title>
          </rect>
        );
      })}
    </svg>
  );
}

export default HealthHistogram;
