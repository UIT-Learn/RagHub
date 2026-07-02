import { useEffect, useState } from 'react';
import { Card, Col, Row, Statistic, Table, Tag, Typography } from 'antd';
import {
  FileTextOutlined, CheckCircleOutlined, CloseCircleOutlined,
  ClockCircleOutlined, DatabaseOutlined,
} from '@ant-design/icons';
import { api, type Document } from '../api';

const { Title } = Typography;

export default function Dashboard() {
  const [docs, setDocs] = useState<Document[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    api.getDocuments()
      .then(setDocs)
      .finally(() => setLoading(false));
  }, []);

  const counts = {
    total:      docs.length,
    indexed:    docs.filter(d => d.status === 'Indexed').length,
    failed:     docs.filter(d => d.status === 'Failed').length,
    pending:    docs.filter(d => d.status === 'Pending' || d.status === 'Processing').length,
    totalChunks: docs.reduce((s, d) => s + d.chunkCount, 0),
  };

  const failed = docs.filter(d => d.status === 'Failed');

  return (
    <div>
      <Title level={3}>Dashboard</Title>

      <Row gutter={16} style={{ marginBottom: 24 }}>
        <Col span={4}><Card><Statistic title="Documents" value={counts.total} prefix={<FileTextOutlined />} /></Card></Col>
        <Col span={4}><Card><Statistic title="Indexed" value={counts.indexed} valueStyle={{ color: '#3f8600' }} prefix={<CheckCircleOutlined />} /></Card></Col>
        <Col span={4}><Card><Statistic title="Failed" value={counts.failed} valueStyle={{ color: counts.failed > 0 ? '#cf1322' : undefined }} prefix={<CloseCircleOutlined />} /></Card></Col>
        <Col span={4}><Card><Statistic title="Pending / Processing" value={counts.pending} prefix={<ClockCircleOutlined />} /></Card></Col>
        <Col span={8}><Card><Statistic title="Total Chunks" value={counts.totalChunks} prefix={<DatabaseOutlined />} /></Card></Col>
      </Row>

      {failed.length > 0 && (
        <>
          <Title level={5} style={{ color: '#cf1322' }}>Failed Documents</Title>
          <Table
            loading={loading}
            dataSource={failed}
            rowKey="id"
            size="small"
            pagination={false}
            columns={[
              { title: 'Name', dataIndex: 'name' },
              { title: 'Category', dataIndex: 'category' },
              {
                title: 'Error',
                dataIndex: 'errorMessage',
                render: (msg: string) => <Tag color="error">{msg}</Tag>,
              },
            ]}
          />
        </>
      )}
    </div>
  );
}
