import { env } from '@/shared/config/env'
import { http } from '@/shared/api/http'
import { listArtifacts as listArtifactsMock } from '@/shared/api/mockDb'
import type { BackupArtifact } from '@/entities/artifact/model/types'

export async function getArtifacts(jobId?: string) {
  if (env.apiMode === 'mock') {
    return listArtifactsMock(jobId)
  }

  const route = jobId
    ? `/api/backupartifacts/job/${jobId}`
    : '/api/backupartifacts'
  const response = await http.get<BackupArtifact[]>(route)
  return response.data
}

function triggerBrowserDownload(blob: Blob, fileName: string) {
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = fileName
  document.body.append(link)
  link.click()
  link.remove()
  URL.revokeObjectURL(url)
}

export async function downloadArtifact(artifact: BackupArtifact) {
  if (env.apiMode === 'mock') {
    const blob = new Blob(
      [
        [
          `Mock artifact: ${artifact.fileName}`,
          `Object key: ${artifact.objectKey}`,
          `Checksum: ${artifact.checksum}`,
        ].join('\n'),
      ],
      { type: 'text/plain;charset=utf-8' },
    )
    triggerBrowserDownload(blob, artifact.fileName)
    return
  }

  const response = await http.get<Blob>(`/api/backupartifacts/${artifact.id}/download`, {
    responseType: 'blob',
  })

  triggerBrowserDownload(response.data, artifact.fileName)
}
