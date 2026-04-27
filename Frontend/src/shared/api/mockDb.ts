import type { Agent, PendingAgent } from '@/entities/agent/model/types'
import type { BackupArtifact } from '@/entities/artifact/model/types'
import type { BackupJob, BackupJobStatus } from '@/entities/job/model/types'
import type {
  BackupPolicy,
  BackupPolicyDatabaseSettings,
  UpsertPolicyInput,
} from '@/entities/policy/model/types'

type DashboardSummary = {
  totalAgents: number
  onlineAgents: number
  staleAgents: number
  offlineAgents: number
  pendingAgents: number
  activePolicies: number
  recentJobs: BackupJob[]
  recentErrors: BackupJob[]
}

const now = Date.now()

const db: {
  agents: Agent[]
  pendingAgents: PendingAgent[]
  policies: BackupPolicy[]
  jobs: BackupJob[]
  artifacts: BackupArtifact[]
} = {
  agents: [
    {
      id: 'a7a9a2d9-08ca-45dd-9018-3d7acdb4d101',
      name: 'Design Laptop',
      machineName: 'DESIGN-WS-01',
      osType: 'Windows 11 Pro',
      version: '0.9.2',
      status: 'online',
      createdAt: new Date(now - 1000 * 60 * 60 * 24 * 21).toISOString(),
      lastSeenAt: new Date(now - 1000 * 45).toISOString(),
    },
    {
      id: '6ad8bf35-8d35-4f8f-9cc4-092eafcb2b02',
      name: 'Accounting Archive',
      machineName: 'ACCT-SRV-02',
      osType: 'Windows Server 2022',
      version: '0.9.1',
      status: 'offline',
      createdAt: new Date(now - 1000 * 60 * 60 * 24 * 45).toISOString(),
      lastSeenAt: new Date(now - 1000 * 60 * 60 * 6).toISOString(),
    },
    {
      id: 'bb65352d-1d27-42c4-b5b7-09ee0d6bd303',
      name: 'QA Mac Mini',
      machineName: 'QA-MINI-03',
      osType: 'macOS Sonoma',
      version: '0.9.3',
      status: 'stale',
      createdAt: new Date(now - 1000 * 60 * 60 * 24 * 12).toISOString(),
      lastSeenAt: new Date(now - 1000 * 60 * 2 - 1000 * 15).toISOString(),
    },
    {
      id: 'dd1248b0-fb4c-4881-90cf-0ad31a61d404',
      name: 'Branch NAS Gateway',
      machineName: 'BRANCH-NODE-07',
      osType: 'Ubuntu 24.04',
      version: '0.9.0',
      status: 'offline',
      createdAt: new Date(now - 1000 * 60 * 60 * 24 * 62).toISOString(),
      lastSeenAt: new Date(now - 1000 * 60 * 60 * 12).toISOString(),
    },
  ],
  pendingAgents: [
    {
      id: '298bef74-9508-4e7f-b7e0-72a29afd5001',
      machineName: 'CEO-LAPTOP',
      osType: 'Windows 11 Pro',
      version: '0.9.3',
      status: 'pending',
      createdAt: new Date(now - 1000 * 60 * 48).toISOString(),
      approvedAgentId: null,
    },
    {
      id: '1940ab31-02b2-4a5a-bf34-f36228ea5002',
      machineName: 'STUDIO-MAC',
      osType: 'macOS Sonoma',
      version: '0.9.2',
      status: 'pending',
      createdAt: new Date(now - 1000 * 60 * 130).toISOString(),
      approvedAgentId: null,
    },
  ],
  policies: [
    {
      id: 'cce21b30-8c5d-4d0c-b3c0-1eb4a5df6001',
      agentId: 'a7a9a2d9-08ca-45dd-9018-3d7acdb4d101',
      type: 'filesystem',
      name: 'Documents every 15 minutes',
      sourcePath: 'C:\\Users\\Designer\\Documents',
      isEnabled: true,
      intervalSeconds: 900,
      nextRunAt: new Date(now + 1000 * 60 * 11).toISOString(),
      lastRunAt: new Date(now - 1000 * 60 * 4).toISOString(),
      createdAt: new Date(now - 1000 * 60 * 60 * 24 * 18).toISOString(),
      databaseSettings: null,
    },
    {
      id: '5351eb46-4117-48de-a6ae-5ec617cb6002',
      agentId: 'a7a9a2d9-08ca-45dd-9018-3d7acdb4d101',
      type: 'filesystem',
      name: 'Project cache nightly',
      sourcePath: 'D:\\Projects\\Cache',
      isEnabled: false,
      intervalSeconds: 86400,
      nextRunAt: new Date(now + 1000 * 60 * 60 * 6).toISOString(),
      lastRunAt: new Date(now - 1000 * 60 * 60 * 18).toISOString(),
      createdAt: new Date(now - 1000 * 60 * 60 * 24 * 10).toISOString(),
      databaseSettings: null,
    },
    {
      id: 'abf77945-fbab-4f38-b32e-f17c67526003',
      agentId: '6ad8bf35-8d35-4f8f-9cc4-092eafcb2b02',
      type: 'filesystem',
      name: 'Finance share hourly',
      sourcePath: '\\\\corp-fs\\finance',
      isEnabled: true,
      intervalSeconds: 3600,
      nextRunAt: new Date(now + 1000 * 60 * 22).toISOString(),
      lastRunAt: new Date(now - 1000 * 60 * 39).toISOString(),
      createdAt: new Date(now - 1000 * 60 * 60 * 24 * 30).toISOString(),
      databaseSettings: null,
    },
    {
      id: 'c0d0ff0c-60fd-4570-aa55-0c0ad2db6004',
      agentId: 'bb65352d-1d27-42c4-b5b7-09ee0d6bd303',
      type: 'filesystem',
      name: 'QA screenshots',
      sourcePath: '/Users/qa/Desktop/screenshots',
      isEnabled: true,
      intervalSeconds: 1800,
      nextRunAt: new Date(now + 1000 * 60 * 9).toISOString(),
      lastRunAt: new Date(now - 1000 * 60 * 20).toISOString(),
      createdAt: new Date(now - 1000 * 60 * 60 * 24 * 8).toISOString(),
      databaseSettings: null,
    },
    {
      id: '14d20b69-e39e-456f-8e25-b4256b826005',
      agentId: 'dd1248b0-fb4c-4881-90cf-0ad31a61d404',
      type: 'filesystem',
      name: 'Branch archive nightly',
      sourcePath: '/srv/archive',
      isEnabled: true,
      intervalSeconds: 86400,
      nextRunAt: new Date(now + 1000 * 60 * 60 * 10).toISOString(),
      lastRunAt: new Date(now - 1000 * 60 * 60 * 25).toISOString(),
      createdAt: new Date(now - 1000 * 60 * 60 * 24 * 41).toISOString(),
      databaseSettings: null,
    },
    {
      id: '7ae0d4f8-7b7d-4a68-9bd0-f38ba85f6010',
      agentId: 'a7a9a2d9-08ca-45dd-9018-3d7acdb4d101',
      type: 'postgres',
      name: 'RestoreMe postgres nightly',
      sourcePath: '',
      isEnabled: true,
      intervalSeconds: 86400,
      nextRunAt: new Date(now + 1000 * 60 * 60 * 7).toISOString(),
      lastRunAt: new Date(now - 1000 * 60 * 60 * 12).toISOString(),
      createdAt: new Date(now - 1000 * 60 * 60 * 24 * 5).toISOString(),
      databaseSettings: {
        engine: 'postgres',
        authMode: 'integrated',
        host: 'localhost',
        port: 5432,
        databaseName: 'restoreme_db',
        username: 'postgres',
        password: null,
      },
    },
  ],
  jobs: [
    {
      id: 'f44a1cb6-db8f-4834-ae33-70a9e9ee7001',
      agentId: 'a7a9a2d9-08ca-45dd-9018-3d7acdb4d101',
      policyId: 'cce21b30-8c5d-4d0c-b3c0-1eb4a5df6001',
      status: 'completed',
      startedAt: new Date(now - 1000 * 60 * 18).toISOString(),
      completedAt: new Date(now - 1000 * 60 * 16).toISOString(),
      errorMessage: null,
    },
    {
      id: '4ff98485-06a2-4a3d-a7bc-a4b3dd267002',
      agentId: 'bb65352d-1d27-42c4-b5b7-09ee0d6bd303',
      policyId: 'c0d0ff0c-60fd-4570-aa55-0c0ad2db6004',
      status: 'running',
      startedAt: new Date(now - 1000 * 60 * 4).toISOString(),
      completedAt: null,
      errorMessage: null,
    },
    {
      id: 'f2fc9664-0ef7-4d04-a385-6835a21a7003',
      agentId: '6ad8bf35-8d35-4f8f-9cc4-092eafcb2b02',
      policyId: 'abf77945-fbab-4f38-b32e-f17c67526003',
      status: 'failed',
      startedAt: new Date(now - 1000 * 60 * 55).toISOString(),
      completedAt: new Date(now - 1000 * 60 * 49).toISOString(),
      errorMessage: 'MinIO upload ticket expired before chunk upload finished.',
    },
    {
      id: 'e84cdc40-e98f-4189-95d8-cd6629eb7004',
      agentId: 'dd1248b0-fb4c-4881-90cf-0ad31a61d404',
      policyId: '14d20b69-e39e-456f-8e25-b4256b826005',
      status: 'completed',
      startedAt: new Date(now - 1000 * 60 * 60 * 3).toISOString(),
      completedAt: new Date(now - 1000 * 60 * 60 * 3 + 1000 * 60 * 12).toISOString(),
      errorMessage: null,
    },
    {
      id: '2ce6d703-cd6b-4d74-bae9-3b4d83db7005',
      agentId: 'a7a9a2d9-08ca-45dd-9018-3d7acdb4d101',
      policyId: '5351eb46-4117-48de-a6ae-5ec617cb6002',
      status: 'failed',
      startedAt: new Date(now - 1000 * 60 * 60 * 26).toISOString(),
      completedAt: new Date(now - 1000 * 60 * 60 * 26 + 1000 * 60 * 9).toISOString(),
      errorMessage: 'Source path became unavailable during snapshot creation.',
    },
  ],
  artifacts: [
    {
      id: '11ebad57-9ec1-48c3-b07b-163123ff8001',
      jobId: 'f44a1cb6-db8f-4834-ae33-70a9e9ee7001',
      fileName: 'documents-2026-04-20-1540.zip',
      objectKey:
        'a7a9a2d9-08ca-45dd-9018-3d7acdb4d101/cce21b30-8c5d-4d0c-b3c0-1eb4a5df6001/f44a1cb6-db8f-4834-ae33-70a9e9ee7001/documents.zip',
      size: 181245937,
      checksum: 'b1f8fca2ad27f8f0a1435fdd1280c2a5',
      createdAt: new Date(now - 1000 * 60 * 16).toISOString(),
    },
    {
      id: 'a7406603-4ed0-43cd-9a15-6bc533d28002',
      jobId: 'e84cdc40-e98f-4189-95d8-cd6629eb7004',
      fileName: 'branch-nightly-2026-04-20.tar.zst',
      objectKey:
        'dd1248b0-fb4c-4881-90cf-0ad31a61d404/14d20b69-e39e-456f-8e25-b4256b826005/e84cdc40-e98f-4189-95d8-cd6629eb7004/nightly.tar.zst',
      size: 891245937,
      checksum: '4f10fd9344d0bc12cd2200ad4e9bfe20',
      createdAt: new Date(now - 1000 * 60 * 60 * 3 + 1000 * 60 * 10).toISOString(),
    },
    {
      id: 'c5904574-adb1-4b30-a4eb-9fd660da8003',
      jobId: '4ff98485-06a2-4a3d-a7bc-a4b3dd267002',
      fileName: 'screenshots-partial-01.zip',
      objectKey:
        'bb65352d-1d27-42c4-b5b7-09ee0d6bd303/c0d0ff0c-60fd-4570-aa55-0c0ad2db6004/4ff98485-06a2-4a3d-a7bc-a4b3dd267002/partial.zip',
      size: 24245937,
      checksum: 'd5f94413ce23956116e7a0de845e11a0',
      createdAt: new Date(now - 1000 * 60 * 2).toISOString(),
    },
  ],
}

function clone<T>(value: T): T {
  return structuredClone(value)
}

function delay<T>(value: T, ms = 220) {
  return new Promise<T>((resolve) => {
    window.setTimeout(() => resolve(clone(value)), ms)
  })
}

function randomId() {
  return crypto.randomUUID()
}

function cloneDatabaseSettings(
  settings: BackupPolicyDatabaseSettings | null,
): BackupPolicyDatabaseSettings | null {
  return settings ? structuredClone(settings) : null
}

export async function getDashboardSummary() {
  const recentJobs = [...db.jobs]
    .sort((left, right) => right.startedAt.localeCompare(left.startedAt))
    .slice(0, 5)

  const recentErrors = recentJobs.filter((job) => job.status === 'failed')

  const summary: DashboardSummary = {
    totalAgents: db.agents.length,
    onlineAgents: db.agents.filter((agent) => agent.status === 'online').length,
    staleAgents: db.agents.filter((agent) => agent.status === 'stale').length,
    offlineAgents: db.agents.filter((agent) => agent.status === 'offline').length,
    pendingAgents: db.pendingAgents.length,
    activePolicies: db.policies.filter((policy) => policy.isEnabled).length,
    recentJobs,
    recentErrors,
  }

  return delay(summary)
}

export async function listAgents() {
  return delay(
    [...db.agents].sort((left, right) =>
      right.createdAt.localeCompare(left.createdAt),
    ),
  )
}

export async function getAgent(agentId: string) {
  const agent = db.agents.find((item) => item.id === agentId) ?? null
  return delay(agent)
}

export async function listPendingAgents() {
  return delay(
    [...db.pendingAgents].sort((left, right) =>
      right.createdAt.localeCompare(left.createdAt),
    ),
  )
}

export async function approvePendingAgent(pendingId: string, name: string) {
  const pendingAgent = db.pendingAgents.find((item) => item.id === pendingId)

  if (!pendingAgent) {
    throw new Error('Pending agent not found')
  }

  const createdAgent: Agent = {
    id: randomId(),
    name,
    machineName: pendingAgent.machineName,
    osType: pendingAgent.osType,
    version: pendingAgent.version,
    status: 'offline',
    createdAt: new Date().toISOString(),
    lastSeenAt: null,
  }

  pendingAgent.status = 'approved'
  pendingAgent.approvedAgentId = createdAgent.id
  db.agents.unshift(createdAgent)
  db.pendingAgents = db.pendingAgents.filter((item) => item.id !== pendingId)

  return delay(createdAgent)
}

export async function listPolicies() {
  return delay(
    [...db.policies].sort((left, right) =>
      right.createdAt.localeCompare(left.createdAt),
    ),
  )
}

export async function getPolicy(policyId: string) {
  return delay(db.policies.find((item) => item.id === policyId) ?? null)
}

export async function createPolicy(input: UpsertPolicyInput) {
  const policy: BackupPolicy = {
    id: randomId(),
    agentId: input.agentId,
    type: input.type,
    name: input.name,
    sourcePath: input.sourcePath,
    isEnabled: input.isEnabled,
    intervalSeconds: input.intervalSeconds,
    nextRunAt: new Date(Date.now() + input.intervalSeconds * 1000).toISOString(),
    lastRunAt: null,
    createdAt: new Date().toISOString(),
    databaseSettings: cloneDatabaseSettings(input.databaseSettings),
  }

  db.policies.unshift(policy)
  return delay(policy)
}

export async function updatePolicy(policyId: string, input: UpsertPolicyInput) {
  const policy = db.policies.find((item) => item.id === policyId)

  if (!policy) {
    throw new Error('Policy not found')
  }

  policy.agentId = input.agentId
  policy.type = input.type
  policy.name = input.name
  policy.sourcePath = input.sourcePath
  policy.intervalSeconds = input.intervalSeconds
  policy.isEnabled = input.isEnabled
  policy.databaseSettings = cloneDatabaseSettings(input.databaseSettings)
  policy.nextRunAt = new Date(Date.now() + input.intervalSeconds * 1000).toISOString()

  return delay(policy)
}

export async function togglePolicy(policyId: string) {
  const policy = db.policies.find((item) => item.id === policyId)

  if (!policy) {
    throw new Error('Policy not found')
  }

  policy.isEnabled = !policy.isEnabled
  return delay(policy)
}

export async function listJobs(status?: BackupJobStatus | 'all') {
  const result =
    status && status !== 'all'
      ? db.jobs.filter((job) => job.status === status)
      : db.jobs

  return delay(
    [...result].sort((left, right) =>
      right.startedAt.localeCompare(left.startedAt),
    ),
  )
}

export async function getJob(jobId: string) {
  return delay(db.jobs.find((item) => item.id === jobId) ?? null)
}

export async function listArtifacts(jobId?: string) {
  const result = jobId
    ? db.artifacts.filter((artifact) => artifact.jobId === jobId)
    : db.artifacts

  return delay(
    [...result].sort((left, right) =>
      right.createdAt.localeCompare(left.createdAt),
    ),
  )
}
