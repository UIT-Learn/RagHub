import { useEffect, useState } from 'react';
import { useParams, Link } from 'react-router-dom';
import {
  Alert, Button, Card, Descriptions, Space, Spin, Table, Tag, Tooltip, Typography, message,
} from 'antd';
import { ReloadOutlined, ArrowLeftOutlined, DownloadOutlined } from '@ant-design/icons';
import { api, type Chunk, type Document } from '../api';

const { Title, Text, Paragraph } = Typography;

const STATUS_COLOR: Record<string, string> = {
  Indexed: 'success', Failed: 'error', Processing: 'processing', Pending: 'default',
};

export default function DocumentDetail() {
  const { id } = useParams<{ id: string }>();
  const docId = Number(id);

  const [doc, setDoc]       = useState<Document | null>(null);
  const [chunks, setChunks] = useState<Chunk[]>([]);
  const [loading, setLoading] = useState(true);
  const [reindexing, setReindexing] = useState(false);
  const [expandedChunk, setExpandedChunk] = useState<number | null>(null);

  const load = async () => {
    setLoading(true);
    try {
      const [d, cs] = await Promise.all([api.getDocument(docId), api.getChunks(docId)]);
      setDoc(d);
      setChunks(cs);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => { load(); }, [docId]);

  async function handleReindex() {
    setReindexing(true);
    try {
      await api.reindex(docId);
      message.success('Reindex queued');
      setTimeout(load, 1500);
    } catch {
      message.error('Reindex failed');
    } finally {
      setReindexing(false);
    }
  }

  async function handleExport() {
    try {
      await api.exportDocument(docId, `${doc!.name.replace(/\.[^.]+$/, '')}_export.json`);
    } catch {
      message.error('Export failed — document must be Indexed first.');
    }
  }

  if (loading) return <Spin />;
  if (!doc)    return <Alert type="error" message="Document not found" />;

  return (
    <div>
      <Space style={{ marginBottom: 16 }}>
        <Link to="/documents"><Button icon={<ArrowLeftOutlined />}>Documents</Button></Link>
        <Title level={3} style={{ margin: 0 }}>{doc.name}</Title>
        <Tag color={STATUS_COLOR[doc.status] ?? 'default'}>{doc.status}</Tag>
        <Button icon={<ReloadOutlined />} loading={reindexing} onClick={handleReindex}>
          Reindex
        </Button>
        <Tooltip title={doc.status !== 'Indexed' ? 'Document must be Indexed to export' : ''}>
          <Button icon={<DownloadOutlined />} disabled={doc.status !== 'Indexed'} onClick={handleExport}>
            Export
          </Button>
        </Tooltip>
      </Space>

      <Card style={{ marginBottom: 24 }}>
        <Descriptions bordered size="small" column={2}>
          <Descriptions.Item label="Category">{doc.category}</Descriptions.Item>
          <Descriptions.Item label="Type">{doc.type || 'auto'}</Descriptions.Item>
          <Descriptions.Item label="Status">
            <Tag color={STATUS_COLOR[doc.status]}>{doc.status}</Tag>
          </Descriptions.Item>
          <Descriptions.Item label="Chunks">{doc.chunkCount}</Descriptions.Item>
          <Descriptions.Item label="Embedding Model">{doc.embeddingModel ?? '—'}</Descriptions.Item>
          <Descriptions.Item label="Embedding Dim">{doc.embeddingDim ?? '—'}</Descriptions.Item>
          <Descriptions.Item label="Uploaded">
            {new Date(doc.uploadedAt).toLocaleString()}
          </Descriptions.Item>
          {doc.errorMessage && (
            <Descriptions.Item label="Error" span={2}>
              <Tag color="error">{doc.errorMessage}</Tag>
            </Descriptions.Item>
          )}
        </Descriptions>
      </Card>

      <Title level={4}>Chunks ({chunks.length})</Title>
      <Table
        dataSource={chunks}
        rowKey="id"
        size="small"
        pagination={{ pageSize: 25 }}
        expandable={{
          expandedRowRender: (chunk: Chunk) => (
            <Paragraph style={{ margin: 0, whiteSpace: 'pre-wrap', fontSize: 12, maxHeight: 200, overflowY: 'auto' }}>
              {chunk.content}
            </Paragraph>
          ),
          expandedRowKeys: expandedChunk != null ? [expandedChunk] : [],
          onExpand: (_, r) =>
            setExpandedChunk(prev => prev === r.id ? null : r.id),
        }}
        columns={[
          { title: '#', dataIndex: 'chunkIndex', width: 50, align: 'right' },
          {
            title: 'Heading Path', dataIndex: 'headingPath',
            render: (h: string | null) => h
              ? <Text style={{ fontSize: 12 }}>{h}</Text>
              : <Text type="secondary">—</Text>,
          },
          { title: 'Page', dataIndex: 'pageNo', width: 60, align: 'center',
            render: (p: number | null) => p ?? '—' },
          { title: 'Tokens', dataIndex: 'tokenCount', width: 70, align: 'right' },
          {
            title: 'Chars', width: 120, align: 'right',
            render: (_: unknown, r: Chunk) =>
              <Text style={{ fontSize: 11 }} type="secondary">{r.charStart}–{r.charEnd}</Text>,
          },
          {
            title: 'Embedding', dataIndex: 'embeddingModel', width: 100,
            render: (m: string | null) => m
              ? <Tooltip title={m}><Tag color="green">✓</Tag></Tooltip>
              : <Tag>none</Tag>,
          },
          {
            title: 'Preview', width: 300,
            render: (_: unknown, r: Chunk) => (
              <Text style={{ fontSize: 11 }} type="secondary">
                {r.content.slice(0, 80).replace(/\n/g, ' ')}…
              </Text>
            ),
          },
        ]}
      />
    </div>
  );
}
