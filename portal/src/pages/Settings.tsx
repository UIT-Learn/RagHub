import { useEffect, useState } from 'react';
import {
  Button, Card, Form, Input, InputNumber, Modal, Popconfirm, Select,
  Space, Switch, Table, Typography, message,
} from 'antd';
import { DeleteOutlined, EditOutlined, PlusOutlined } from '@ant-design/icons';
import { api, type ChunkingProfile, type RetrievalConfig } from '../api';

const { Title, Text } = Typography;

const STRATEGIES = [
  { value: 'auto', label: 'Auto-detect' },
  { value: 'policy', label: 'Policy (numbered headings)' },
  { value: 'technical', label: 'Technical (markdown headings)' },
  { value: 'api', label: 'API Spec (HTTP verbs)' },
];

export default function Settings() {
  const [profiles, setProfiles] = useState<ChunkingProfile[]>([]);
  const [loadingProfiles, setLoadingProfiles] = useState(true);
  const [editing, setEditing] = useState<ChunkingProfile | 'new' | null>(null);
  const [profileForm] = Form.useForm();

  const [retrieval, setRetrieval] = useState<RetrievalConfig | null>(null);
  const [retrievalForm] = Form.useForm();
  const [savingRetrieval, setSavingRetrieval] = useState(false);

  const loadProfiles = () => {
    setLoadingProfiles(true);
    api.getChunkingProfiles().then(setProfiles).finally(() => setLoadingProfiles(false));
  };

  useEffect(loadProfiles, []);
  useEffect(() => {
    api.getRetrievalConfig().then(cfg => {
      setRetrieval(cfg);
      retrievalForm.setFieldsValue(cfg);
    });
  }, [retrievalForm]);

  function openCreate() {
    profileForm.resetFields();
    profileForm.setFieldsValue({ strategy: 'auto', maxChunkSize: 1500, overlap: 100 });
    setEditing('new');
  }

  function openEdit(p: ChunkingProfile) {
    profileForm.setFieldsValue(p);
    setEditing(p);
  }

  async function handleProfileSubmit(values: { name: string; strategy: string; maxChunkSize: number; overlap: number }) {
    try {
      if (editing === 'new') {
        await api.createChunkingProfile(values.name, values.strategy, values.maxChunkSize, values.overlap);
        message.success('Profile created');
      } else if (editing) {
        await api.updateChunkingProfile(editing.id, values.name, values.strategy, values.maxChunkSize, values.overlap);
        message.success('Profile updated');
      }
      setEditing(null);
      loadProfiles();
    } catch (e: unknown) {
      message.error((e as Error).message ?? 'Save failed');
    }
  }

  async function handleDeleteProfile(id: number) {
    try {
      const res = await api.deleteChunkingProfile(id);
      if (!res.ok) {
        const body = await res.json().catch(() => null);
        throw new Error(body?.error ?? 'Delete failed');
      }
      message.success('Profile deleted');
      loadProfiles();
    } catch (e: unknown) {
      message.error((e as Error).message ?? 'Delete failed');
    }
  }

  async function handleRetrievalSubmit(values: Omit<RetrievalConfig, 'updatedAt'>) {
    setSavingRetrieval(true);
    try {
      const cfg = await api.updateRetrievalConfig(values);
      setRetrieval(cfg);
      message.success('Retrieval settings saved — applies to next query immediately');
    } catch (e: unknown) {
      message.error((e as Error).message ?? 'Save failed');
    } finally {
      setSavingRetrieval(false);
    }
  }

  return (
    <div>
      <Title level={3}>Settings</Title>

      <Card
        title="Chunking Profiles"
        extra={<Button icon={<PlusOutlined />} onClick={openCreate}>New Profile</Button>}
        style={{ marginBottom: 24 }}
      >
        <Text type="secondary">
          Used at upload/reindex time. Changing a profile only affects documents indexed afterwards.
        </Text>
        <Table
          style={{ marginTop: 12 }}
          loading={loadingProfiles}
          dataSource={profiles}
          rowKey="id"
          size="small"
          pagination={false}
          columns={[
            { title: 'Name', dataIndex: 'name' },
            { title: 'Strategy', dataIndex: 'strategy' },
            { title: 'Max Chunk Size', dataIndex: 'maxChunkSize', align: 'right' },
            { title: 'Overlap', dataIndex: 'overlap', align: 'right' },
            {
              title: 'Actions',
              render: (_: unknown, p: ChunkingProfile) => (
                <Space>
                  <Button size="small" icon={<EditOutlined />} onClick={() => openEdit(p)} />
                  <Popconfirm
                    title="Delete profile?"
                    description="Fails if any document still references it."
                    onConfirm={() => handleDeleteProfile(p.id)}
                  >
                    <Button size="small" danger icon={<DeleteOutlined />} />
                  </Popconfirm>
                </Space>
              ),
            },
          ]}
        />
      </Card>

      <Card title="Retrieval — live tuning">
        <Text type="secondary">
          Applies to every query immediately — no reindex needed.
        </Text>
        {retrieval && (
          <Form
            form={retrievalForm}
            layout="vertical"
            onFinish={handleRetrievalSubmit}
            style={{ marginTop: 12, maxWidth: 420 }}
          >
            <Form.Item name="candidateK" label="Candidate K (pre-rerank pool size)" rules={[{ required: true }]}>
              <InputNumber min={1} style={{ width: '100%' }} />
            </Form.Item>
            <Form.Item name="finalN" label="Final N (chunks sent to LLM)" rules={[{ required: true }]}>
              <InputNumber min={1} style={{ width: '100%' }} />
            </Form.Item>
            <Form.Item name="useHybrid" label="Use Hybrid (dense + sparse + RRF)" valuePropName="checked">
              <Switch />
            </Form.Item>
            <Form.Item name="useReranker" label="Use Reranker (cross-encoder sidecar)" valuePropName="checked">
              <Switch />
            </Form.Item>
            <Form.Item
              name="useMultiQuery"
              label="Use Multi-Query (paraphrase question, fuse results — off by default, adds latency)"
              valuePropName="checked"
            >
              <Switch />
            </Form.Item>
            <Form.Item name="multiQueryCount" label="Multi-Query: number of paraphrases">
              <InputNumber min={1} max={5} style={{ width: '100%' }} />
            </Form.Item>
            <Button type="primary" htmlType="submit" loading={savingRetrieval}>
              Save
            </Button>
          </Form>
        )}
      </Card>

      <Modal
        title={editing === 'new' ? 'New Chunking Profile' : 'Edit Chunking Profile'}
        open={editing !== null}
        onCancel={() => setEditing(null)}
        onOk={() => profileForm.submit()}
        destroyOnClose
      >
        <Form form={profileForm} layout="vertical" onFinish={handleProfileSubmit}>
          <Form.Item name="name" label="Name" rules={[{ required: true, message: 'Required' }]}>
            <Input placeholder="e.g. policy-tight" />
          </Form.Item>
          <Form.Item name="strategy" label="Strategy" rules={[{ required: true }]}>
            <Select options={STRATEGIES} />
          </Form.Item>
          <Form.Item name="maxChunkSize" label="Max Chunk Size (chars)" rules={[{ required: true }]}>
            <InputNumber min={100} style={{ width: '100%' }} />
          </Form.Item>
          <Form.Item name="overlap" label="Overlap (chars)" rules={[{ required: true }]}>
            <InputNumber min={0} style={{ width: '100%' }} />
          </Form.Item>
        </Form>
      </Modal>
    </div>
  );
}
