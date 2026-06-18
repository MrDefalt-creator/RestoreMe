import {
  Area,
  AreaChart,
  CartesianGrid,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts'

import { formatFileSize } from '@/shared/lib/format'

/**
 * Cumulative storage growth area chart for the dashboard. Bytes are
 * formatted on both axis ticks and the tooltip so operators never see
 * raw "1.2e+09" values. Colours come from CSS variables so the chart
 * follows the active theme without re-rendering.
 */
type Point = {
  date: string
  cumulativeBytes: number
}

type StorageGrowthChartProps = {
  data: Point[]
  seriesLabel: string
}

export function StorageGrowthChart({ data, seriesLabel }: StorageGrowthChartProps) {
  return (
    <ResponsiveContainer width="100%" height={220}>
      <AreaChart data={data} margin={{ top: 8, right: 12, bottom: 4, left: 4 }}>
        <defs>
          <linearGradient id="storageGrowthFill" x1="0" y1="0" x2="0" y2="1">
            <stop offset="0%" stopColor="hsl(var(--primary))" stopOpacity={0.35} />
            <stop offset="100%" stopColor="hsl(var(--primary))" stopOpacity={0.02} />
          </linearGradient>
        </defs>
        <CartesianGrid stroke="hsl(var(--border))" strokeDasharray="3 3" vertical={false} />
        <XAxis
          dataKey="date"
          stroke="hsl(var(--muted-foreground))"
          fontSize={12}
          tickLine={false}
          axisLine={false}
          tickFormatter={shortDate}
          minTickGap={24}
        />
        <YAxis
          stroke="hsl(var(--muted-foreground))"
          fontSize={12}
          tickLine={false}
          axisLine={false}
          tickFormatter={(value) => formatFileSize(value as number)}
          width={64}
        />
        <Tooltip
          cursor={{ stroke: 'hsl(var(--muted-foreground))', strokeDasharray: '3 3' }}
          contentStyle={{
            background: 'hsl(var(--card))',
            border: '1px solid hsl(var(--border))',
            borderRadius: 8,
            fontSize: 12,
            color: 'hsl(var(--foreground))',
          }}
          labelStyle={{ color: 'hsl(var(--muted-foreground))' }}
          formatter={(value) => [formatFileSize(value as number), seriesLabel]}
        />
        <Area
          type="monotone"
          dataKey="cumulativeBytes"
          stroke="hsl(var(--primary))"
          strokeWidth={2}
          fill="url(#storageGrowthFill)"
        />
      </AreaChart>
    </ResponsiveContainer>
  )
}

function shortDate(value: string) {
  // Date payload is yyyy-MM-dd from the backend — keep MM-DD which is
  // unambiguous and fits in a tiny axis tick.
  return value.length >= 10 ? value.slice(5) : value
}
