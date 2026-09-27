// Time ranges for the QoS Monitor page. QoS admission history is kept in memory for 24 hours and a
// single history query may not exceed 24 hours, so the ranges stop at one day. Buckets are aligned to
// the interval in UTC (the server aligns history buckets to the Unix epoch the same way).
export { floorToStep, getRangeWindow } from './requestHistoryTimeRanges';

export const QOS_TIME_RANGES = [
  { label: 'Last 15 Minutes', value: '15m', interval: 'minute', stepMs: 60_000, bucketCount: 15 },
  { label: 'Last Hour', value: 'hour', interval: 'minute', stepMs: 60_000, bucketCount: 60 },
  { label: 'Last 6 Hours', value: '6h', interval: '5minute', stepMs: 300_000, bucketCount: 72 },
  { label: 'Last Day', value: 'day', interval: '15minute', stepMs: 900_000, bucketCount: 96 }
];

export const DEFAULT_QOS_TIME_RANGE = 'hour';

export function getQosTimeRange(value) {
  return QOS_TIME_RANGES.find((entry) => entry.value === value) || QOS_TIME_RANGES[1];
}
