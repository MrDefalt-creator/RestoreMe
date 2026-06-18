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
 * Horizontal bar chart for the top failing policies. Horizontal layout
 * keeps long policy names readable without truncation tricks. Height
 * grows with row count so 1-bar and 5-bar versions both look balanced.
 */
type Point = {
  policyName: string
  failureCount: number
}

type TopFailingPoliciesChartProps = {
  data: Point[]
  seriesLabel: string
}

export function TopFailingPoliciesChart({ data, seriesLabel }: TopFailingPoliciesChartProps) {
  const rowHeight = 40
  const verticalPadding = 56
  const height = Math.max(160, data.length * rowHeight + verticalPadding)

  return (
    <ResponsiveContainer width="100%" height={height}>
      <BarChart
        layout="vertical"
        data={data}
        margin={{ top: 8, right: 16, bottom: 4, left: 12 }}
      >
        <CartesianGrid stroke="hsl(var(--border))" strokeDasharray="3 3" horizontal={false} />
        <XAxis
          type="number"
          stroke="hsl(var(--muted-foreground))"
          fontSize={12}
          tickLine={false}
          axisLine={false}
          allowDecimals={false}
        />
        <YAxis
          type="category"
          dataKey="policyName"
          stroke="hsl(var(--muted-foreground))"
          fontSize={12}
          tickLine={false}
          axisLine={false}
          width={148}
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
        <Bar
          dataKey="failureCount"
          fill="hsl(var(--destructive))"
          radius={[0, 4, 4, 0]}
          maxBarSize={24}
        />
      </BarChart>
    </ResponsiveContainer>
  )
}
