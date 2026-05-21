import {
  Bar,
  BarChart,
  CartesianGrid,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts'

/**
 * Compact bar chart for the dashboard "Backup activity trend" widget.
 * Hands every colour off to a CSS variable (--primary, --border,
 * --muted-foreground, --card) so the chart automatically follows the active
 * theme (light / dark / system) without re-rendering.
 */
type Point = {
  label: string
  value: number
}

type TrendBarChartProps = {
  data: Point[]
  /** Optional accessible label for the bar series. Default: "jobs". */
  seriesLabel?: string
}

export function TrendBarChart({ data, seriesLabel = 'jobs' }: TrendBarChartProps) {
  return (
    <ResponsiveContainer width="100%" height={200}>
      <BarChart data={data} margin={{ top: 8, right: 8, bottom: 4, left: -16 }}>
        <CartesianGrid stroke="hsl(var(--border))" strokeDasharray="3 3" vertical={false} />
        <XAxis
          dataKey="label"
          stroke="hsl(var(--muted-foreground))"
          fontSize={12}
          tickLine={false}
          axisLine={false}
        />
        <YAxis
          stroke="hsl(var(--muted-foreground))"
          fontSize={12}
          tickLine={false}
          axisLine={false}
          allowDecimals={false}
          width={32}
        />
        <Tooltip
          cursor={{ fill: 'hsl(var(--secondary) / 0.4)' }}
          contentStyle={{
            background: 'hsl(var(--card))',
            border: '1px solid hsl(var(--border))',
            borderRadius: 8,
            fontSize: 12,
            color: 'hsl(var(--foreground))',
          }}
          labelStyle={{ color: 'hsl(var(--muted-foreground))' }}
          formatter={(value) => [value as number, seriesLabel]}
        />
        <Bar dataKey="value" fill="hsl(var(--primary))" radius={[4, 4, 0, 0]} maxBarSize={36} />
      </BarChart>
    </ResponsiveContainer>
  )
}
