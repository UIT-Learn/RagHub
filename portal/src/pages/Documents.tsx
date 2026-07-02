import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { Button, Popconfirm, Select, Space, Table, Tag, Tooltip, Typography, message } from 'antd';
import { DeleteOutlined, ReloadOutlined, UploadOutlined } from '@ant-design/icons';
import { api, type ChunkingProfile, type Document } from '../api';

const { Title } = Typography;

const STATUS_COLOR: Record<string, string> = {
  Indexed:    'success',
  Failed:     'error',
  Processing: 'processing',
  Pending:    'default',
};

export default function Documents() {
  const [docs, setDocs]     = useState<Document[]>([]);
  const [loading, setLoading] = useState(true);
  const [reindexing, setReindexing] = useState<Set<number>>(new Set());
  const [deleting, setDeleting]     = useState<Set<number>>(new Set());
  const [profiles, setProfiles]     = useState<ChunkingProfile[]>([]);
  const [reindexProfile, setReindexProfile] = useState<Record<number, number | undefined>>({});

  const load = () => {
    setLoading(true);
    api.getDocuments().then(setDocs).finally(() => setLoading(false));
  };

  useEffect(load, []);
  useEffect(() => { api.getChunkingProfiles().then(setProfiles).catch(() => setProfiles([])); }, []);

  async function handleDelete(id: number) {
    setDeleting(prev => new Set(prev).add(id));
    try {
      const res = await api.deleteDocument(id);
      if (!res.ok) throw new Error();
      message.success('Document deleted');
      load();
    } catch {
      message.error('Delete failed');
    } finally {
      setDeleting(prev => { const s = new Set(prev); s.delete(id); return s; });
    }
  }

  async function handleReindex(id: number) {
    setReindexing(prev => new Set(prev).add(id));
    try {
      await api.reindex(id, reindexProfile[id]);
      message.success('Reindex queued');
      setTimeout(load, 1500);
    } catch {
      message.error('Reindex failed');
    } finally {
      setReindexing(prev => { const s = new Set(prev); s.delete(id); return s; });
    }
  }

  return (
    <div>
      <Space style={{ marginBottom: 16 }} align="center">
        <Title level={3} style={{ margin: 0 }}>Documents</Title>
        <Link to="/upload"><Button type="primary" icon={<UploadOutlined />}>Upload</Button></Link>
        <Button icon={<ReloadOutlined />} onClick={load}>Refresh</Button>
      </Space>

      <Table
        loading={loading}
        dataSource={docs}
        rowKey="id"
        size="small"
        pagination={{ pageSize: 20 }}
        columns={[
          {
            title: 'Name', dataIndex: 'name',
            render: (name: string, d: Document) => <Link to={`/documents/${d.id}`}>{name}</Link>,
          },
          { title: 'Category', dataIndex: 'category' },
          { title: 'Type', dataIndex: 'type', render: (t: string) => t || '—' },
          {
            title: 'Status', dataIndex: 'status',
            render: (s: string) => <Tag color={STATUS_COLOR[s] ?? 'default'}>{s}</Tag>,
          },
          { title: 'Chunks', dataIndex: 'chunkCount', align: 'right' },
          {
            title: 'Chunk Config',
            render: (_: unknown, d: Document) => d.chunkSizeUsed
              ? <Tooltip title={`profile #${d.chunkingProfileId ?? '—'}`}>
                  <Tag>{d.chunkSizeUsed}/{d.overlapUsed}</Tag>
                </Tooltip>
              : '—',
          },
          {
            title: 'Uploaded', dataIndex: 'uploadedAt',
            render: (t: string) => new Date(t).toLocaleString(),
          },
          {
            title: 'Error', dataIndex: 'errorMessage',
            render: (msg: string | null) => msg
              ? <Tooltip title={msg}><Tag color="error">Error</Tag></Tooltip>
              : null,
          },
          {
            title: 'Actions',
            render: (_: unknown, d: Document) => (
              <Space>
                <Select
                  size="small"
                  style={{ width: 110 }}
                  placeholder="profile"
                  value={reindexProfile[d.id] ?? d.chunkingProfileId ?? undefined}
                  onChange={v => setReindexProfile(prev => ({ ...prev, [d.id]: v }))}
                  options={profiles.map(p => ({ value: p.id, label: p.name }))}
                  disabled={d.status === 'Processing'}
                />
                <Button
                  size="small"
                  icon={<ReloadOutlined />}
                  loading={reindexing.has(d.id)}
                  onClick={() => handleReindex(d.id)}
                  disabled={d.status === 'Processing'}
                >
                  Reindex
                </Button>
                <Popconfirm
                  title="Delete document?"
                  description="This will remove all chunks and embeddings."
                  onConfirm={() => handleDelete(d.id)}
                  okText="Delete"
                  okButtonProps={{ danger: true }}
                  cancelText="Cancel"
                  disabled={d.status === 'Processing'}
                >
                  <Button
                    size="small"
                    danger
                    icon={<DeleteOutlined />}
                    loading={deleting.has(d.id)}
                    disabled={d.status === 'Processing'}
                  >
                    Delete
                  </Button>
                </Popconfirm>
              </Space>
            ),
          },
        ]}
      />
    </div>
  );
}
