export interface Artifact {
  id: string
  name: string
  jobId: string
  size: number
  type: 'filesystem' | 'postgres' | 'mysql'
  createdAt: string
  expiresAt: string
  downloadUrl: string
}
