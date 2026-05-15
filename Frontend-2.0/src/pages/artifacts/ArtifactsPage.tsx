import { useMemo, useState, type ReactNode } from 'react'
import { useMutation, useQuery } from '@tanstack/react-query'
import {
  AlertTriangle,
  CalendarClock,
  Database,
  FileArchive,
  FolderArchive,
  HardDriveDownload,
  RefreshCw,
  Search,
} from 'lucide-react'
import { toast } from 'sonner'

import { downloadArtifact, getArtifacts, type Artifact } from '@/shared/api/artifacts'
import { queryKeys } from '@/shared/lib/query'
import { formatDateTime, formatFileSize, formatRelativeTime, formatPolicyType } from '@/shared/lib/format'
import { Badge } from '@/shared/ui/Badge'
import { Button } from '@/shared/ui/Button'
import { Card, CardContent } from '@/shared/ui/Card'
import { EmptyState } from '@/shared/ui/EmptyState'
import { Input } from '@/shared/ui/Input'
import { SectionHeading } from '@/shared/ui/SectionHeading'
import { Spinner } from '@/shared/ui/Spinner'
import { useI18n } from '@/shared/i18n'
import { useLiveQueryOptions } from '@/shared/lib/useLiveQueryOptions'

type ArtifactType = 'filesystem' | 'postgres' | 'mysql'
type TypeFilter = 'all' | ArtifactType

const EMPTY_ARTIFACTS: Artifact[] = []

export function ArtifactsPage() {
  const { t } = useI18n()
  const liveQueryOptions = useLiveQueryOptions()
  const [query, setQuery] = useState('')
  const [typeFilter, setTypeFilter] = useState<TypeFilter>('all')
  const [downloadingId, setDownloadingId] = useState<string | null>(null)

  const artifactsQuery = useQuery({
    queryKey: queryKeys.artifacts,
    queryFn: getArtifacts,
    ...liveQueryOptions,
  })

  const downloadMutation = useMutation({
    mutationFn: async (artifact: Artifact) => {
      setDownloadingId(artifact.id)
      const blob = await downloadArtifact(artifact.id)
      const url = URL.createObjectURL(blob)
      const link = document.createElement('a')
      link.href = url
      link.download = getArtifactDisplayName(artifact)
      document.body.appendChild(link)
      link.click()
      link.remove()
      window.setTimeout(() => URL.revokeObjectURL(url), 0)
    },
    onSuccess: (_, artifact) => {
      toast.success(t('{name} download started', { name: getArtifactDisplayName(artifact) }))
    },
    onError: (error) => {
      toast.error(error instanceof Error ? error.message : t('Artifact download failed'))
    },
    onSettled: () => setDownloadingId(null),
  })

  const artifacts = artifactsQuery.data ?? EMPTY_ARTIFACTS
  const normalizedQuery = query.trim().toLowerCase()

  const filteredArtifacts = useMemo(() => {
    return artifacts.filter((artifact) => {
      const artifactType = getArtifactType(artifact)
      const matchesType = typeFilter === 'all' || artifactType === typeFilter
      const searchable = [
        artifact.name,
        artifact.fileName,
        artifact.objectKey,
        artifact.checksum,
        artifactType,
        artifact.jobId,
        artifact.id,
      ]
        .filter(Boolean)
        .join(' ')
        .toLowerCase()

      return matchesType && (!normalizedQuery || searchable.includes(normalizedQuery))
    })
  }, [artifacts, normalizedQuery, typeFilter])

  const stats = useMemo(
    () => ({
      total: artifacts.length,
      totalSize: artifacts.reduce((sum, artifact) => sum + artifact.size, 0),
      filesystem: artifacts.filter((artifact) => getArtifactType(artifact) === 'filesystem').length,
      database: artifacts.filter((artifact) => getArtifactType(artifact) !== 'filesystem').length,
    }),
    [artifacts],
  )

  return (
    <div className="space-y-7">
      <SectionHeading
        eyebrow={t('Recovery')}
        title={t('Backups')}
        description={t('A clean recovery shelf for every completed backup: inspect type, age, retention, and download the exact artifact you need.')}
        action={
          <Button variant="secondary" onClick={() => artifactsQuery.refetch()} disabled={artifactsQuery.isFetching}>
            <RefreshCw className={artifactsQuery.isFetching ? 'h-4 w-4 animate-spin' : 'h-4 w-4'} />
            {t('Refresh')}
          </Button>
        }
      />

      <div className="grid gap-3 md:grid-cols-4">
        <ArtifactMetric icon={<FileArchive />} label={t('Artifacts')} value={stats.total.toString()} />
        <ArtifactMetric icon={<HardDriveDownload />} label={t('Stored size')} value={formatFileSize(stats.totalSize)} />
        <ArtifactMetric icon={<FolderArchive />} label={t('Filesystem')} value={stats.filesystem.toString()} />
        <ArtifactMetric icon={<Database />} label={t('Database')} value={stats.database.toString()} />
      </div>

      <Card>
        <CardContent className="flex flex-col gap-3 lg:flex-row lg:items-center">
          <div className="relative flex-1">
            <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
            <Input
              value={query}
              onChange={(event) => setQuery(event.target.value)}
              placeholder={t('Search by artifact, filename, type, job, or id...')}
              className="pl-10"
            />
          </div>
          <div className="flex rounded-lg border border-border bg-secondary/50 p-1">
            {(['all', 'filesystem', 'postgres', 'mysql'] as TypeFilter[]).map((type) => (
              <button
                key={type}
                type="button"
                onClick={() => setTypeFilter(type)}
                className={
                  typeFilter === type
                    ? 'rounded-md bg-card px-3 py-2 text-sm font-medium text-foreground shadow-sm transition'
                    : 'rounded-md px-3 py-2 text-sm font-medium text-muted-foreground transition hover:text-foreground'
                }
              >
                {type === 'all' ? t('all') : formatPolicyType(type)}
              </button>
            ))}
          </div>
        </CardContent>
      </Card>

      {artifactsQuery.isLoading ? (
        <Card>
          <CardContent className="flex min-h-64 items-center justify-center gap-3 text-muted-foreground">
            <Spinner />
            {t('Loading artifacts...')}
          </CardContent>
        </Card>
      ) : artifactsQuery.isError ? (
        <EmptyState
          icon={<AlertTriangle className="h-8 w-8 text-warning" />}
          title={t('Artifacts could not be loaded')}
          description={t('Check the backend and object storage containers, then retry this shelf.')}
          action={
            <Button variant="secondary" onClick={() => artifactsQuery.refetch()}>
              {t('Retry')}
            </Button>
          }
        />
      ) : filteredArtifacts.length ? (
        <Card className="overflow-hidden">
          <CardContent className="p-0">
            <div className="divide-y divide-border">
              {filteredArtifacts.map((artifact) => (
                <ArtifactRow
                  key={artifact.id}
                  artifact={artifact}
                  isDownloading={downloadingId === artifact.id}
                  onDownload={() => downloadMutation.mutate(artifact)}
                  t={t}
                />
              ))}
            </div>
          </CardContent>
        </Card>
      ) : (
        <EmptyState
          icon={<HardDriveDownload className="h-8 w-8 text-muted-foreground" />}
          title={artifacts.length ? t('No artifacts match these filters') : t('No artifacts yet')}
          description={
            artifacts.length
              ? t('Clear the search or switch the artifact type.')
              : t('Successful backups will appear here as downloadable recovery artifacts.')
          }
        />
      )}
    </div>
  )
}

function ArtifactMetric({
  icon,
  label,
  value,
}: {
  icon: ReactNode
  label: string
  value: string
}) {
  return (
    <Card>
      <CardContent className="flex items-center justify-between gap-3">
        <div className="min-w-0">
          <p className="text-sm text-muted-foreground">{label}</p>
          <p className="mt-1 truncate text-2xl font-semibold text-foreground">{value}</p>
        </div>
        <div className="flex h-11 w-11 shrink-0 items-center justify-center rounded-lg bg-secondary text-muted-foreground">
          {icon}
        </div>
      </CardContent>
    </Card>
  )
}

function ArtifactRow({
  artifact,
  isDownloading,
  onDownload,
  t,
}: {
  artifact: Artifact
  isDownloading: boolean
  onDownload: () => void
  t: (key: string, params?: Record<string, string | number>) => string
}) {
  const expiresAt = Date.parse(artifact.expiresAt ?? '')
  const hasExpiry = Boolean(artifact.expiresAt) && Number.isFinite(expiresAt)
  const isExpired = hasExpiry && Date.now() > expiresAt
  const expiresSoon = hasExpiry && !isExpired && expiresAt - Date.now() < 3 * 24 * 60 * 60 * 1000
  const displayName = getArtifactDisplayName(artifact)
  const artifactType = getArtifactType(artifact)

  return (
    <div className="grid gap-4 p-4 transition hover:bg-secondary/35 lg:grid-cols-[1.35fr_1fr_auto] lg:items-center">
      <div className="flex min-w-0 items-center gap-3">
        <div
          className={
            isExpired || expiresSoon
              ? 'flex h-12 w-12 shrink-0 items-center justify-center rounded-lg bg-warning/12 text-warning'
              : 'flex h-12 w-12 shrink-0 items-center justify-center rounded-lg bg-primary/10 text-primary'
          }
        >
          <FileArchive className="h-5 w-5" />
        </div>
        <div className="min-w-0">
          <div className="flex flex-wrap items-center gap-2">
            <p className="truncate font-medium text-foreground">{displayName}</p>
            <Badge variant={artifactType === 'filesystem' ? 'neutral' : 'accent'}>
              {formatPolicyType(artifactType)}
            </Badge>
          </div>
          <p className="mt-1 truncate text-sm text-muted-foreground">
            {t('Created')} {formatRelativeTime(artifact.createdAt)} | {formatDateTime(artifact.createdAt)}
          </p>
        </div>
      </div>

      <div className="grid grid-cols-2 gap-3 text-sm">
        <ArtifactFact label={t('Size')} value={formatFileSize(artifact.size)} />
        <ArtifactFact
          label={isExpired ? t('Expired') : t('Expires')}
          value={hasExpiry ? (isExpired ? t('Unavailable soon') : formatRelativeTime(artifact.expiresAt as string)) : t('Retained')}
          warning={isExpired || expiresSoon}
        />
      </div>

      <div className="flex items-center gap-3 lg:justify-end">
        {isExpired || expiresSoon ? (
          <div className="hidden items-center gap-2 text-sm text-warning sm:flex">
            <CalendarClock className="h-4 w-4" />
            {isExpired ? t('Expired') : t('Expiring')}
          </div>
        ) : null}
        <Button
          variant={isExpired ? 'secondary' : 'primary'}
          size="sm"
          onClick={onDownload}
          disabled={isDownloading || isExpired}
          title={isExpired ? t('This artifact is expired') : undefined}
        >
          <HardDriveDownload className="h-4 w-4" />
          {isDownloading ? t('Downloading...') : t('Download')}
        </Button>
      </div>
    </div>
  )
}

function getArtifactDisplayName(artifact: Artifact) {
  return artifact.fileName || artifact.name || artifact.objectKey?.split('/').pop() || `Artifact ${shortId(artifact.id)}`
}

function getArtifactType(artifact: Artifact): ArtifactType {
  if (artifact.type) {
    return artifact.type
  }

  const name = `${artifact.fileName ?? ''} ${artifact.name ?? ''} ${artifact.objectKey ?? ''}`.toLowerCase()
  if (name.includes('postgres') || name.endsWith('.sql') || name.endsWith('.dump')) {
    return 'postgres'
  }
  if (name.includes('mysql')) {
    return 'mysql'
  }

  return 'filesystem'
}

function shortId(id: string | undefined) {
  return id ? id.slice(0, 8) : 'unknown'
}

function ArtifactFact({
  label,
  value,
  warning = false,
}: {
  label: string
  value: string
  warning?: boolean
}) {
  return (
    <div className="min-w-0">
      <p className="text-xs text-muted-foreground">{label}</p>
      <p className={warning ? 'truncate font-medium text-warning' : 'truncate font-medium text-foreground'}>
        {value}
      </p>
    </div>
  )
}
