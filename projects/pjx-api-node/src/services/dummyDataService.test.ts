import 'mocha';
import { expect } from 'chai';
import DummyDataService from '../../src/services/dummyDataService';

describe('', () => {
  it('id 1 should return one item', () => {
    const result = DummyDataService.getDummyDataById(1).length;
    expect(result).to.be.eqls(1);
  });
});
